using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using Clc.Sip.Tests.TestSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using sip_console;
using ConsoleProgram = sip_console.Program;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class ConsoleTests
{
    [TestMethod(DisplayName = "Console JSON and field aliases")]
    public void ConsoleJsonSupportsFieldAliases()
    {
        var input = """
            {"Host":"127.0.0.1","Port":5002,"Commands":[{"Sequence":1,"Name":"Login","Method":"93","FixedFields":"00","VariableFields":{"sipUsername":"test-user","sipPassword":"test-pass"}}]}
            """;
        var sequence = JsonConvert.DeserializeObject<SipConsoleCommandSequence>(input);
        Assert.AreEqual(1, sequence.Commands.Count);
        Assert.AreEqual("9300CNtest-user|COtest-pass|", sequence.Commands[0].ToCommandString());
        Assert.IsTrue(JObject.FromObject(sequence)["Sequence"] == null, "Sequence must be ignored by Newtonsoft.Json.");
    }

    [TestMethod(DisplayName = "Console PRTG output and escaping")]
    public void PrtgOutputEscapesCommandNames()
    {
        var command = new SipConsoleCommand { Name = "Status & <test>", SuccessRegex = "^98Y" };
        var xml = XDocument.Parse(ConsoleProgram.FormatResults(new[] { new SipResponse(command, "98Y", 0) }));
        Assert.AreEqual(command.Name, xml.Root.Element("result").Element("channel").Value);
        Assert.AreEqual("1", xml.Root.Element("result").Element("value").Value);
        Assert.AreEqual("prtg.standardlookups.boolean.statetrueok", xml.Root.Element("result").Element("valuelookup").Value);
    }

    [TestMethod(DisplayName = "Console usage")]
    public void ConsoleReportsUsageAndSupportsHelp()
    {
        var (exit, output) = RunConsole(Array.Empty<string>());
        Assert.AreEqual(2, exit);
        Assert.IsTrue(output.StartsWith("Usage:"), "Missing usage text.");
        Assert.AreEqual(0, RunConsole(new[] { "--help" }).ExitCode);
    }

    [TestMethod(DisplayName = "Console end to end")]
    public void ConsoleSubmitsCommandAndReportsPrtgResult()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var request = SipRequest.ReadFrom(peer);
            peer.GetStream().Write(Encoding.ASCII.GetBytes("98YAY0AZFDD1\r"));
            return request;
        });
        var sequence = new SipConsoleCommandSequence("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        sequence.AddCommand(new SipConsoleCommand { Name = "ACS & status", Method = "99", FixedFields = "01002.00", SuccessRegex = "^98Y" });
        var (exit, output) = RunConsole(new[] { JsonConvert.SerializeObject(sequence) });
        Assert.AreEqual(0, exit);
        var result = XDocument.Parse(output).Root.Element("result");
        Assert.AreEqual("ACS & status", result.Element("channel").Value);
        Assert.AreEqual("1", result.Element("value").Value);
        Assert.IsTrue(server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult().StartsWith("9901002.00|AY0AZ"), "Unexpected console request.");
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
}
