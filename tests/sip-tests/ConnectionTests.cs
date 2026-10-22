using System.Net;
using System.Net.Sockets;
using System.Text;
using Clc.Sip;
using Clc.Sip.Tests.TestSupport;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class ConnectionTests
{
    [TestMethod(DisplayName = "ACS Status over TCP")]
    public void AcsStatusExchangesRequestAndResponseOverTcp()
    {
        var (txn, request) = Exchange("98YAY0AZFDD1\r");
        Assert.AreEqual("9901002.00AY0AZFCA8\r", request);
        Assert.AreEqual("98YAY0AZFDD1\r", txn.Response);
        Assert.IsFalse(txn.ConnectionReset);
        Assert.IsTrue(txn.ChecksumValid);
    }

    [TestMethod(DisplayName = "Peer closes connection")]
    public void PeerClosingConnectionReturnsResetTransaction()
    {
        var (txn, _) = Exchange(null);
        Assert.IsTrue(txn.ConnectionReset);
        Assert.IsFalse(txn.ChecksumValid);
        Assert.AreEqual("", txn.Response);
    }

    [TestMethod(DisplayName = "Peer resets connection")]
    public void PeerResettingConnectionReturnsResetTransaction()
    {
        var (txn, _) = Exchange(null, reset: true);
        Assert.IsTrue(txn.ConnectionReset);
        Assert.IsFalse(txn.ChecksumValid);
    }

    [TestMethod(DisplayName = "Checksum setting is honored")]
    public void ResponseChecksumValidationCanBeDisabled()
    {
        Assert.IsFalse(Exchange("98YAY0AZ0000\r").Transaction.ChecksumValid);
        Assert.IsTrue(Exchange("98YAY0AZ0000\r", sip => sip.ValidateResponseChecksum = false).Transaction.ChecksumValid);
    }

    [TestMethod(DisplayName = "Encoding setting is honored over TCP")]
    public void RequestsAndResponsesUseConfiguredEncoding()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var request = SipRequest.ReadFrom(peer, Encoding.Latin1);
            peer.GetStream().Write(Encoding.Latin1.GetBytes("98\u00e9AY0AZ0000\r"));
            return request;
        });
        using var sip = new sip2("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port)
        {
            TextEncoding = Encoding.Latin1,
            ValidateResponseChecksum = false
        };
        sip.Connect();
        var txn = sip.Custom("AB\u00e9");
        Assert.AreEqual("AB\u00e9AY0AZFD2F\r", server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult());
        Assert.AreEqual("98\u00e9AY0AZ0000\r", txn.Response);
    }

    private static (SipTransaction Transaction, string Request) Exchange(string response, Action<sip2> configure = null, bool reset = false)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var request = SipRequest.ReadFrom(peer);
            if (reset)
            {
                peer.LingerState = new LingerOption(true, 0);
            }

            if (response != null)
            {
                peer.GetStream().Write(Encoding.UTF8.GetBytes(response));
            }

            return request;
        });
        using var sip = new sip2("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        configure?.Invoke(sip);
        sip.Connect();
        var transaction = sip.AcsStatus();
        return (transaction, server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult());
    }
}
