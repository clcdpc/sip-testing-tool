using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Clc.Sip.Tests.TestSupport;

internal sealed class CommandProbe : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentQueue<string> messages = new();
    private readonly Task worker;
    private TcpClient peer;
    private volatile bool disposed;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public string[] Messages => messages.ToArray();

    public CommandProbe()
    {
        listener.Start();
        worker = Task.Run(() =>
        {
            try
            {
                using var accepted = listener.AcceptTcpClient();
                peer = accepted;
                accepted.ReceiveTimeout = 15000;
                var stream = accepted.GetStream();
                while (!disposed)
                {
                    using var bytes = new MemoryStream();
                    int next;
                    while ((next = stream.ReadByte()) >= 0)
                    {
                        bytes.WriteByte((byte)next);
                        if (next == '\r')
                        {
                            break;
                        }
                    }
                    if (bytes.Length == 0)
                    {
                        break;
                    }

                    messages.Enqueue(Encoding.UTF8.GetString(bytes.ToArray()));
                    stream.Write(Encoding.ASCII.GetBytes("98YAY0AZFDD1\r"));
                }
            }
            catch (Exception ex) when (disposed && ex is IOException or SocketException or ObjectDisposedException) { }
        });
    }

    public sip2 ConnectClient()
    {
        var client = new sip2("127.0.0.1", Port, "institution", "terminal");
        try
        {
            client.Connect();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public string CaptureSingleRequest(sip2 client)
    {
        var captured = Messages;
        Assert.AreEqual(1, captured.Length, "The command should submit exactly one request.");
        Assert.IsTrue(client.CheckChecksum(captured[0]), "The captured request checksum is invalid.");
        return captured[0];
    }

    public void Dispose()
    {
        disposed = true;
        peer?.Dispose();
        listener.Stop();
        worker.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
}
