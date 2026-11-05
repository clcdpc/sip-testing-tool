using System.Net.Sockets;
using System.Text;

namespace Clc.Sip.Tests.TestSupport;

internal static class SipRequest
{
    // AY + sequence digit + AZ + four checksum digits + CR.
    private const int TrailerLength = 10;

    public static string Payload(string message) => message[..^TrailerLength];

    public static string VariableFields(string message, int fixedLength) => message.Substring(fixedLength, message.Length - fixedLength - TrailerLength);

    public static string ReadFrom(TcpClient client, Encoding encoding = null)
    {
        client.ReceiveTimeout = 5000;
        client.SendTimeout = 5000;
        using var bytes = new MemoryStream();
        int value;
        while ((value = client.GetStream().ReadByte()) >= 0)
        {
            bytes.WriteByte((byte)value);
            if (value == '\r')
            {
                break;
            }
        }
        return (encoding ?? Encoding.UTF8).GetString(bytes.ToArray());
    }
}
