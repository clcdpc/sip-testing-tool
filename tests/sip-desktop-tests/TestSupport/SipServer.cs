using System.Net;
using System.Net.Sockets;
using System.Text;
using Clc.Sip;

namespace Clc.Sip.Desktop.Tests.TestSupport;

internal sealed class SipServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(15));
    public int Port { get; }
    public Task Completion { get; }
    public SipServer(Func<NetworkStream, CancellationToken, Task> handler)
    {
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Completion = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
            await handler(client.GetStream(), cancellation.Token);
        });
    }
    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();
        try
        {
            await Completion;
        }
        catch (OperationCanceledException) { }
        cancellation.Dispose();
    }

    public static async Task<string> ReadMessage(NetworkStream stream, CancellationToken token)
    {
        var bytes = new List<byte>();
        var buffer = new byte[1];
        while (await stream.ReadAsync(buffer, token) != 0)
        {
            bytes.Add(buffer[0]);
            if (buffer[0] == '\r')
            {
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
        }
        throw new EndOfStreamException("Client closed before sending a complete SIP request.");
    }

    public static Task WriteMessage(NetworkStream stream, string body, CancellationToken token)
    {
        var prefix = body + "AY0AZ";
        return stream.WriteAsync(Encoding.UTF8.GetBytes(prefix + sip2.CalculateChecksum(prefix) + "\r"), token).AsTask();
    }
}
