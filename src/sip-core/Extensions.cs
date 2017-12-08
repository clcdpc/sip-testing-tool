using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Drawing;
using System.IO;
using System.Configuration;

namespace Clc.Sip
{
	public static class Extensions
	{
		public static void WriteToStream(this TcpClient clientSocket, string message)
		{
			NetworkStream serverStream = clientSocket.GetStream();
			byte[] outStream = Encoding.GetEncoding(ConfigurationManager.AppSettings["encoding"]).GetBytes(message);
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
					fullServerReply = Encoding.GetEncoding(ConfigurationManager.AppSettings["encoding"]).GetString(writer.ToArray());
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
