using Clc.Sip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace sip_console
{
    public class SipConsoleCommandSequence
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string EncodingName { get; set; } = "UTF-8";
        public bool CheckChecksum { get; set; } = true;

        int _seq = -1;

        [JsonIgnore]
        public int Sequence => _seq == 9 ? 0 : ++_seq;

        public SipConsoleCommandSequence()
        {
        }

        public SipConsoleCommandSequence(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public List<SipConsoleCommand> Commands { get; private set; } = new List<SipConsoleCommand>();

        public List<SipConsoleCommand> AddCommand(SipConsoleCommand command)
        {
            if (!command.Sequence.HasValue)
            {
                command.Sequence = (Commands.OrderByDescending(c => c.Sequence).FirstOrDefault()?.Sequence ?? 0) + 1;
            }

            Commands.Add(command);
            return Commands;
        }

        public List<SipResponse> Run()
        {
            var output = new List<SipResponse>();

            if (string.IsNullOrWhiteSpace(Host) || Port < 1 || Port > 65535)
                throw new ArgumentException("Host and a port between 1 and 65535 are required.");
            using var sip = new sip2(Host, Port)
            {
                TextEncoding = Encoding.GetEncoding(EncodingName),
                ValidateResponseChecksum = CheckChecksum
            };
            sip.Connect();

            foreach (var cmd in Commands.OrderBy(c => c.Sequence))
            {
                var cmdString = cmd.ToCommandString();
                var txn = sip.Custom(cmdString);
                if (txn.ConnectionReset) throw new System.IO.IOException("The SIP server closed the connection.");
                output.Add(new SipResponse(cmd, txn.Response, txn.ResponseTime));
            }

            return output.OrderBy(o => o.Command.Sequence).ToList();
        }
    }
}
