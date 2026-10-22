using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace Clc.Sip.Tests.TestSupport;

internal static class SipRequest
{
    // AY + sequence digit + AZ + four checksum digits + CR.
    private const int TrailerLength = 10;
    private const int DateFieldLength = 18;

    public static string Payload(string message) => message[..^TrailerLength];

    public static string VariableFields(string message, int fixedLength) => message.Substring(fixedLength, message.Length - fixedLength - TrailerLength);

    public static void AssertTimestamp(string message, int offset, DateTime before, DateTime after)
    {
        var field = message.Substring(offset, DateFieldLength);
        Assert.IsTrue(DateTime.TryParseExact(field, "yyyyMMdd    HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value), "The timestamp is not a SIP date/time.");
        Assert.IsTrue(value >= before.AddSeconds(-1) && value <= after.AddSeconds(1), "The timestamp must come from this submission.");
    }

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
