using System.Xml.Linq;
using Newtonsoft.Json;

namespace sip_console;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine("Usage: sip-console <commands.json | inline-json>");
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var input = args[0].TrimStart();
            var json = input.StartsWith('{') ? input : File.ReadAllText(args[0]);
            var sequence = JsonConvert.DeserializeObject<SipConsoleCommandSequence>(json)
                ?? throw new ArgumentException("The input must contain a SIP command sequence.");
            Console.WriteLine(FormatResults(sequence.Run()));
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(new XElement("prtg", new XElement("error", 1), new XElement("text", ex.Message)));
            return 1;
        }
    }

    public static string FormatResults(IEnumerable<SipResponse> results)
    {
        return new XElement("prtg", results.Select(result =>
            new XElement("result",
                new XElement("channel", result.Command.Name),
                new XElement("value", result.IsSuccess ? 1 : 0),
                new XElement("valuelookup", "prtg.standardlookups.boolean.statetrueok")))).ToString();
    }
}
