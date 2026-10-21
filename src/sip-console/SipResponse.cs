using System.Text.RegularExpressions;

namespace sip_console;

public class SipResponse
{
    public SipConsoleCommand Command { get; set; }
    public string Received { get; set; }
    public long ResponseTime { get; set; }
    public bool IsSuccess => string.IsNullOrWhiteSpace(Command.SuccessRegex) || Regex.IsMatch(Received, Command.SuccessRegex);

    public SipResponse()
    {
    }

    public SipResponse(SipConsoleCommand command, string received, long responseTime)
    {
        Command = command;
        Received = received;
        ResponseTime = responseTime;
    }

    public override string ToString() => $"{Command.Name} - {IsSuccess} - {Received}";
}
