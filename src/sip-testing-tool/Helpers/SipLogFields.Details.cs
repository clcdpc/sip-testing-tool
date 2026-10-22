namespace Clc.Sip.Helpers;

public enum SipLogValueState { Normal, Set, Invalid }

public sealed record SipLogDetail(int Offset, int Length, string Name, string Description, string Value,
    string Meaning = null, IReadOnlyList<SipLogDetail> Children = null, SipLogValueState State = SipLogValueState.Normal)
{
    public string DisplayValue => (Value.Length == 0 ? "(empty)" : Value.All(c => c == ' ') ? $"({Value.Length} spaces)" : Value) +
        (Meaning == null ? "" : "\n" + Meaning);
}

public sealed record SipLogMessageDetails(string Name, IReadOnlyList<SipLogDetail> Fields);

public static partial class SipLogFields
{
    public static SipLogMessageDetails DescribeMessage(string message)
    {
        message ??= "";
        var fields = new List<SipLogDetail>();
        if (message.Length < 2 || !char.IsAsciiDigit(message[0]) || !char.IsAsciiDigit(message[1]))
        {
            fields.Add(new(0, message.Length, "Unrecognized message", "A SIP message should start with a two-digit message ID.", message,
                State: SipLogValueState.Invalid));
            return new("Unrecognized message", fields);
        }

        var offset = SipFixedFields.ReadDetails(message, fields, out var name);
        if (offset < 0)
        {
            name = "Unknown message (" + message[..2] + ")";
            fields.Add(new(0, 2, "Message ID", "This message ID is not in the built-in SIP2 reference.", message[..2]));
            var delimiter = message.IndexOf('|');
            var headerEnd = delimiter < 0 ? message.Length : delimiter;
            if (headerEnd > 2)
            {
                fields.Add(new(2, headerEnd - 2, "Unrecognized header", "The fixed layout is unknown; this data is shown without guessing its fields.", message[2..headerEnd]));
            }
            offset = delimiter < 0 ? message.Length : delimiter + 1;
        }

        while (offset < message.Length)
        {
            var end = message.IndexOf('|', offset);
            if (end < 0)
            {
                end = message.Length;
            }
            // The sequence digit is followed directly by the checksum identifier.
            if (message.AsSpan(offset).StartsWith("AY", StringComparison.Ordinal) &&
                offset + 5 <= end && message.AsSpan(offset + 3, 2).SequenceEqual("AZ"))
            {
                end = offset + 3;
            }
            if (end > offset)
            {
                if (end - offset < 2 || !char.IsAsciiLetterUpper(message[offset]) || !char.IsAsciiLetterUpper(message[offset + 1]))
                {
                    fields.Add(new(offset, end - offset, "Unrecognized data", "Expected a two-letter field identifier.", message[offset..end],
                        State: SipLogValueState.Invalid));
                }
                else
                {
                    var identifier = message.Substring(offset, 2);
                    var value = message[(offset + 2)..end];
                    var detail = SipFixedFields.ReadIdentifiedDetail(identifier, value, offset);
                    if (detail == null)
                    {
                        var description = descriptions.GetValueOrDefault(identifier,
                            "Unrecognized field\nThis code is not in the built-in SIP2 reference; it may be a server-specific extension.");
                        var parts = description.Split('\n', 2);
                        detail = new(offset, end - offset, identifier + " · " + parts[0], parts[1], value);
                    }
                    fields.Add(detail);
                }
            }
            offset = end < message.Length && message[end] == '|' ? end + 1 : end;
        }
        return new(name, fields);
    }
}
