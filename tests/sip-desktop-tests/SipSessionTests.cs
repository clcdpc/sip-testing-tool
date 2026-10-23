using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.Tests.TestSupport;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;
using static Clc.Sip.Desktop.Tests.TestSupport.SipServer;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
[TestCategory("Avalonia")]
public sealed class SipSessionTests
{
    [TestMethod(DisplayName = "All twelve commands use the shared SIP client")]
    public async Task AllCommandsApplySharedFieldsOverTcp()
    {
        var commands = CommandChoice.All;
        var received = new List<string>();
        await using var server = new SipServer(async (stream, token) =>
        {
            foreach (var command in commands)
            {
                var request = await ReadMessage(stream, token);
                received.Add(request);
                Assert.IsTrue(request.StartsWith(command.Code, StringComparison.Ordinal), "Wrong command: " + request);
                await WriteMessage(stream, "941", token);
            }
        });
        using var session = new SipSession();
        var fields = SampleFields();
        fields.Server = "127.0.0.1";
        fields.Port = server.Port.ToString();
        await session.ConnectAsync(fields, "UTF-8", true);
        var inputs = new CommandFields
        {
            Summary = "charged",
            FeeType = "4",
            PaymentType = "0",
            Amount = "1.25",
            FeeIdentifier = "FEE-7",
            TransactionId = "TX-8",
            NoticeComment = "Test notice"
        };
        foreach (var command in commands)
        {
            var transaction = await session.ExecuteAsync(command.Command, fields, inputs, "UTF-8", true);
            Assert.IsTrue(!transaction.ConnectionReset && transaction.ChecksumValid, "Valid response was rejected.");
            Assert.IsTrue(transaction.Message.EndsWith('\r'), "The raw request terminator is missing.");
            Assert.IsTrue(transaction.Response.StartsWith("941", StringComparison.Ordinal), "The raw response is missing.");
        }
        await server.Completion;
        var messages = commands.Select((command, index) => (command.Command, Message: received[index])).ToDictionary(x => x.Command, x => x.Message);
        foreach (var command in commands.Where(c => c.Command is not (SipCommand.Login or SipCommand.AcsStatus)))
        {
            Assert.IsTrue(messages[command.Command].Contains("AOCLC|"), "Institution did not reach " + command.Name);
        }

        Assert.IsTrue(messages[SipCommand.Checkout].Contains("AAP123|ABI456|"), "Checkout lost shared barcodes.");
        Assert.IsTrue(messages[SipCommand.Checkin].Contains("APdesk-1|"), "Checkin lost the terminal.");
        Assert.IsTrue(messages[SipCommand.PatronInformation].Contains("BP6|BQ10|"), "Patron range was not applied.");
        Assert.IsTrue(messages[SipCommand.FeePaid].Contains("CGFEE-7|BKTX-8|"), "Payment identifiers were not applied.");
        Assert.IsTrue(messages[SipCommand.NoticeUpdate].Contains("DETest notice|"), "Notice comment was not applied.");
        Assert.IsTrue(messages[SipCommand.Login].Contains("CNtest-user|COtest-secret|CPMAIN|VPITG|"), "Login lost existing optional fields.");
    }

    [TestMethod(DisplayName = "Concurrent commands are serialized")]
    public async Task ConcurrentCommandsAreSerialized()
    {
        await using var server = new SipServer(async (stream, token) =>
        {
            for (var i = 0; i < 2; i++)
            {
                _ = await ReadMessage(stream, token);
                await Task.Delay(40, token);
                Assert.IsFalse(stream.DataAvailable, "A second request overlapped the first.");
                await WriteMessage(stream, "941", token);
            }
        });
        using var session = new SipSession();
        var fields = SampleFields();
        fields.Server = "127.0.0.1";
        fields.Port = server.Port.ToString();
        await session.ConnectAsync(fields, "UTF-8", true);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => session.ExecuteAsync(SipCommand.AcsStatus, fields, new(), "UTF-8", true)));
        Assert.IsTrue(results.All(r => r.ChecksumValid && !r.ConnectionReset), "Serialized requests failed.");
        await server.Completion;
    }

    [TestMethod(DisplayName = "Connection timeout releases the operation")]
    public async Task TimeoutDisconnectsAndReleasesOperation()
    {
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            var buffer = new byte[1];
            _ = await stream.ReadAsync(buffer, token); // Wait for the client to close its stalled connection.
        });
        using var session = new SipSession(TimeSpan.FromMilliseconds(300));
        var fields = SampleFields();
        fields.Server = "127.0.0.1";
        fields.Port = server.Port.ToString();
        await session.ConnectAsync(fields, "UTF-8", true);
        try
        {
            var transaction = await session.ExecuteAsync(SipCommand.AcsStatus, fields, new(), "UTF-8", true);
            Assert.IsTrue(transaction.ConnectionReset, "A timed-out transaction should close the connection.");
        }
        catch (TimeoutException) { }
        Assert.IsFalse(session.IsConnected, "A timed-out session remained connected.");
        await server.Completion;
    }

    [TestMethod]
    public async Task PendingConnectionTimesOutEvenIfTheConnectorDoesNotAcknowledgeCancellation()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sip2? abandoned = null;
        CancellationToken connectToken = default;
        using var session = new SipSession((connection, token) =>
        {
            abandoned = connection;
            connectToken = token;
            return pending.Task;
        }, connectTimeout: TimeSpan.FromMilliseconds(200));
        var timer = Stopwatch.StartNew();
        try
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => session.ConnectAsync(SampleFields(), "UTF-8", true)
                .WaitAsync(TimeSpan.FromSeconds(3)));
            StringAssert.Contains(error.Message, "sip.example.test:5002");
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(2), "A pending connect ignored its deadline.");
            Assert.IsTrue(connectToken.IsCancellationRequested);
            Assert.IsFalse(session.IsConnected);
            Assert.AreEqual("", session.Endpoint);
            Assert.IsNotNull(abandoned);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await abandoned.ConnectAsync());
        }
        finally { pending.TrySetResult(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellingOrDisposingAPendingConnectFinishesPromptly(bool dispose)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        using var session = new SipSession((_, token) =>
        {
            started.TrySetResult();
            return Task.Delay(Timeout.Infinite, token);
        });
        var operation = session.ConnectAsync(SampleFields(), "UTF-8", true, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        if (dispose) { session.Dispose(); } else { cancellation.Cancel(); }
        await Assert.ThrowsAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.IsFalse(session.IsConnected);
        Assert.AreEqual("", session.Endpoint);
    }

    [TestMethod]
    public async Task AConnectionTimeoutCanBeRetriedWithAWorkingServer()
    {
        var attempts = 0;
        using var session = new SipSession((connection, token) => Interlocked.Increment(ref attempts) == 1
            ? Task.Delay(Timeout.Infinite, token) : connection.ConnectAsync(token).AsTask(),
            connectTimeout: TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAsync<TimeoutException>(() => session.ConnectAsync(SampleFields(), "UTF-8", true));
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        var fields = SampleFields();
        fields.Server = "127.0.0.1";
        fields.Port = server.Port.ToString();
        await session.ConnectAsync(fields, "UTF-8", true);
        var result = await session.ExecuteAsync(SipCommand.Login, fields, new(), "UTF-8", true);
        Assert.IsTrue(session.IsConnected && result.ChecksumValid && !result.ConnectionReset);
        await server.Completion;
    }

    [TestMethod]
    [DataRow(SocketError.HostNotFound, "resolve server")]
    [DataRow(SocketError.ConnectionRefused, "refused")]
    [DataRow(SocketError.NetworkUnreachable, "network or VPN")]
    public async Task ConnectionFailuresExplainTheTargetAndAllowRetry(SocketError error, string explanation)
    {
        var attempts = 0;
        using var session = new SipSession((connection, token) => Interlocked.Increment(ref attempts) == 1
            ? Task.FromException(new SocketException((int)error)) : connection.ConnectAsync(token).AsTask());
        var failure = await Assert.ThrowsAsync<IOException>(() => session.ConnectAsync(SampleFields(), "UTF-8", true));
        StringAssert.Contains(failure.Message, "sip.example.test");
        StringAssert.Contains(failure.Message, explanation);
        Assert.IsFalse(session.IsConnected);
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        var fields = SampleFields();
        fields.Server = "127.0.0.1";
        fields.Port = server.Port.ToString();
        await session.ConnectAsync(fields, "UTF-8", true);
        Assert.IsTrue((await session.ExecuteAsync(SipCommand.Login, fields, new(), "UTF-8", true)).ChecksumValid);
        await server.Completion;
    }

    [TestMethod]
    public async Task RefusedTcpPortReturnsWithoutWaitingForTheConnectionDeadline()
    {
        using var unavailable = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        unavailable.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var fields = SampleFields();
        fields.Server = "127.0.0.1";
        fields.Port = ((IPEndPoint)unavailable.LocalEndPoint!).Port.ToString();
        using var session = new SipSession();
        var failure = await Assert.ThrowsAsync<IOException>(() => session.ConnectAsync(fields, "UTF-8", true)
            .WaitAsync(TimeSpan.FromSeconds(3)));
        StringAssert.Contains(failure.Message, "refused");
        Assert.IsFalse(session.IsConnected);
    }

    [TestMethod(DisplayName = "Invalid input is rejected before sending")]
    public async Task InvalidInputIsRejectedBeforeSending()
    {
        using var session = new SipSession();
        var fields = SampleFields();
        fields.Port = "70000";
        await Assert.ThrowsAsync<ArgumentException>(() => session.ConnectAsync(fields, "UTF-8", true));
        await Assert.ThrowsAsync<ArgumentException>(() => session.ExecuteAsync(SipCommand.FeePaid, fields,
            new()
            {
                FeeType = "4",
                PaymentType = "x",
                Amount = "1.25"
            }, "UTF-8", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync(SipCommand.Checkout, fields, new(), "UTF-8", true));
    }
}
