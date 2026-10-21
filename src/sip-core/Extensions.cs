using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Clc.Sip
{
    public static class Extensions
    {
        public static void WriteToStream(this TcpClient clientSocket, string message, Encoding encoding = null)
        {
            NetworkStream serverStream = clientSocket.GetStream();
            byte[] outStream = (encoding ?? Encoding.UTF8).GetBytes(message);
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
        }

        public static string ReadFromStream(this TcpClient clientSocket, Encoding encoding = null)
        {
            var networkStream = clientSocket.GetStream();
            if (networkStream.CanRead)
            {
                // Buffer to store the response bytes.
                byte[] readBuffer = new byte[clientSocket.ReceiveBufferSize];
                string fullServerReply = null;
                using (var writer = new MemoryStream())
                {
                    do
                    {
                        int numberOfBytesRead = networkStream.Read(readBuffer, 0, readBuffer.Length);
                        if (numberOfBytesRead <= 0)
                        {
                            break;
                        }
                        writer.Write(readBuffer, 0, numberOfBytesRead);
                    } while (networkStream.DataAvailable);
                    fullServerReply = (encoding ?? Encoding.UTF8).GetString(writer.ToArray());
                    return fullServerReply;
                }
            }
            return "";
        }

        public static string ToSipString(this DateTime dt)
        {
            return dt.ToString("yyyyMMdd    HHmmss");
        }

        public static string ToY_N(this bool b)
        {
            return b ? "Y" : "N";
        }
    }
}
