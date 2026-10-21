using Clc.Sip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using sip_console;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using ConsoleProgram = sip_console.Program;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var tests = new (string Name, Action Run)[]
        {
            ("Known SIP checksum", () => Equal("FCA8", sip2.CalculateChecksum("9901002.00AY0AZ"))),
            ("UTF-8 checksum", () => Equal("FCAC", sip2.CalculateChecksum("AB\u00e9AY0AZ"))),
            ("Supplementary Unicode checksum", () => Equal("FB71", sip2.CalculateChecksum("AB\U0001F600AY0AZ"))),
            ("Configured encoding checksum", () => Equal("FD2F", sip2.CalculateChecksum("AB\u00e9AY0AZ", Encoding.Latin1))),
            ("Short and empty responses", ShortResponses),
            ("Valid and invalid checksums", ResponseChecksums),
            ("Sequence number wraps", SequenceWrap),
            ("ACS Status over TCP", AcsStatus),
            ("Peer closes connection", PeerCloses),
            ("Peer resets connection", PeerResets),
            ("Checksum setting is honored", OptionalChecksum),
            ("Encoding setting is honored over TCP", ConfiguredEncoding),
            ("Console JSON and field aliases", ConsoleJson),
            ("Console PRTG output and escaping", PrtgOutput),
            ("Console usage", ConsoleUsage),
            ("Console end to end", ConsoleEndToEnd),
            ("Windows forms and resources", Forms)
        };
        int failed = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {name}: {ex.GetBaseException().Message}");
            }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}; received {actual}.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static void ShortResponses()
    {
        using var sip = new sip2();
        foreach (var value in new[] { null, "", " ", "98", "\r\n98\r\n", "AZ0000" })
            Equal(false, sip.CheckChecksum(value));
    }

    private static void ResponseChecksums()
    {
        using var sip = new sip2();
        Equal(true, sip.CheckChecksum("98YAY0AZFDD1\r\n"));
        Equal(false, sip.CheckChecksum("98YAY0AZ0000\r"));
        Equal(true, sip.CheckChecksum("98YAY0AZfdd1\r"));
    }

    private static void SequenceWrap()
    {
        using var sip = new sip2();
        for (int i = 0; i < 12; i++) Equal(i % 10, sip.GetSeqNumber());
    }

    private static string ReadRequest(TcpClient client, Encoding encoding = null)
    {
        client.ReceiveTimeout = 5000;
        client.SendTimeout = 5000;
        using var bytes = new MemoryStream();
        int value;
        while ((value = client.GetStream().ReadByte()) >= 0)
        {
            bytes.WriteByte((byte)value);
            if (value == '\r') break;
        }
        return (encoding ?? Encoding.UTF8).GetString(bytes.ToArray());
    }

    private static (SipTransaction Transaction, string Request) Exchange(string response, Action<sip2> configure = null, bool reset = false)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var request = ReadRequest(peer);
            if (reset) peer.LingerState = new LingerOption(true, 0);
            if (response != null) peer.GetStream().Write(Encoding.UTF8.GetBytes(response));
            return request;
        });
        using var sip = new sip2("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        configure?.Invoke(sip);
        sip.Connect();
        var transaction = sip.AcsStatus();
        return (transaction, server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult());
    }

    private static void AcsStatus()
    {
        var (txn, request) = Exchange("98YAY0AZFDD1\r");
        Equal("9901002.00AY0AZFCA8\r", request);
        Equal("98YAY0AZFDD1\r", txn.Response);
        Equal(false, txn.ConnectionReset);
        Equal(true, txn.ChecksumValid);
    }

    private static void PeerCloses()
    {
        var (txn, _) = Exchange(null);
        Equal(true, txn.ConnectionReset);
        Equal(false, txn.ChecksumValid);
        Equal("", txn.Response);
    }

    private static void PeerResets()
    {
        var (txn, _) = Exchange(null, reset: true);
        Equal(true, txn.ConnectionReset);
        Equal(false, txn.ChecksumValid);
    }

    private static void OptionalChecksum()
    {
        Equal(false, Exchange("98YAY0AZ0000\r").Transaction.ChecksumValid);
        Equal(true, Exchange("98YAY0AZ0000\r", sip => sip.ValidateResponseChecksum = false).Transaction.ChecksumValid);
    }

    private static void ConfiguredEncoding()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var request = ReadRequest(peer, Encoding.Latin1);
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
        Equal("AB\u00e9AY0AZFD2F\r", server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult());
        Equal("98\u00e9AY0AZ0000\r", txn.Response);
    }

    private static void ConsoleJson()
    {
        var input = """
            {"Host":"127.0.0.1","Port":5002,"Commands":[{"Sequence":1,"Name":"Login","Method":"93","FixedFields":"00","VariableFields":{"sipUsername":"test-user","sipPassword":"test-pass"}}]}
            """;
        var sequence = JsonConvert.DeserializeObject<SipConsoleCommandSequence>(input);
        Equal(1, sequence.Commands.Count);
        Equal("9300CNtest-user|COtest-pass|", sequence.Commands[0].ToCommandString());
        Check(JObject.FromObject(sequence)["Sequence"] == null, "Sequence must be ignored by Newtonsoft.Json.");
    }

    private static void PrtgOutput()
    {
        var command = new SipConsoleCommand { Name = "Status & <test>", SuccessRegex = "^98Y" };
        var xml = XDocument.Parse(ConsoleProgram.FormatResults(new[] { new SipResponse(command, "98Y", 0) }));
        Equal(command.Name, xml.Root.Element("result").Element("channel").Value);
        Equal("1", xml.Root.Element("result").Element("value").Value);
        Equal("prtg.standardlookups.boolean.statetrueok", xml.Root.Element("result").Element("valuelookup").Value);
    }

    private static (int ExitCode, string Output) RunConsole(string[] args)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            return (ConsoleProgram.Main(args), output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    private static void ConsoleUsage()
    {
        var (exit, output) = RunConsole(Array.Empty<string>());
        Equal(2, exit);
        Check(output.StartsWith("Usage:"), "Missing usage text.");
        Equal(0, RunConsole(new[] { "--help" }).ExitCode);
    }

    private static void ConsoleEndToEnd()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var request = ReadRequest(peer);
            peer.GetStream().Write(Encoding.ASCII.GetBytes("98YAY0AZFDD1\r"));
            return request;
        });
        var sequence = new SipConsoleCommandSequence("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        sequence.AddCommand(new SipConsoleCommand { Name = "ACS & status", Method = "99", FixedFields = "01002.00", SuccessRegex = "^98Y" });
        var (exit, output) = RunConsole(new[] { JsonConvert.SerializeObject(sequence) });
        Equal(0, exit);
        var result = XDocument.Parse(output).Root.Element("result");
        Equal("ACS & status", result.Element("channel").Value);
        Equal("1", result.Element("value").Value);
        Check(server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult().StartsWith("9901002.00|AY0AZ"), "Unexpected console request.");
    }

    private static void Forms()
    {
        using var form = new MainForm();
        Equal("SIP Testing Tool", form.Text);
        Check(form.Icon != null, "Main form icon resource is missing.");
        var acs = form.Controls.Find("btnAcsStatus", true).Single() as Button;
        Check(acs != null && acs.Text == "ACS Status" && !acs.Enabled, "ACS Status must start disabled.");
        var notice = form.Controls.Find("noticeUpdateButton", true).Single();
        Check(!acs.Bounds.IntersectsWith(notice.Bounds), "Restored ACS Status overlaps Update Notice.");
        Equal("5002", form.Controls.Find("portTextBox", true).Single().Text);
        ((ToolStripMenuItem)form.ContextMenuStrip.Items[0]).PerformClick();
        Equal(true, form.TopMost);
        using var settings = new SettingsForm();
        using var noticeForm = new NoticeUpdateForm(form);
        Check(settings.Controls.Count > 0 && noticeForm.Controls.Count > 0, "Supporting form resources are missing.");
    }
}
