using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace sip_net_core
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new TcpClient("devpac", 5001);
            var sip = new sip2("trainpac", 5002, "7", "GHP");

            sip.Connect();
            var foo = sip.Login("3MLogin", "8d7v9t3wyR2zUGJ3CYa3kv5Q");
            Console.WriteLine("Hello World!");
        }
    }

    public static class Extensions
    {
        public static void WriteToStream(this TcpClient clientSocket, string message)
        {
            NetworkStream serverStream = clientSocket.GetStream();
            byte[] outStream = Encoding.UTF8.GetBytes(message);
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
        }

        public static string ReadFromStream(this TcpClient clientSocket)
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
                    fullServerReply = Encoding.UTF8.GetString(writer.ToArray());
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
