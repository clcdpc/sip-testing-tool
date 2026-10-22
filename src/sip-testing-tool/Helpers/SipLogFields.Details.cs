namespace Clc.Sip.Helpers;

public enum SipLogValueState { Normal, Set, Invalid }

public sealed record SipLogDetail(int Offset, int Length, string Name, string Description, string Value,
    string Meaning = null, IReadOnlyList<SipLogDetail> Children = null, SipLogValueState State = SipLogValueState.Normal, bool IsFlag = false)
{
    public string DisplayValue => IsFlag ? Meaning : (Value.Length == 0 ? "(empty)" : Value.All(c => c == ' ') ? $"({Value.Length} spaces)" : Value) +
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
            var end = VariableFieldEnd(message, offset);
            if (end > offset)
            {
                if (end - offset < 2 || !IsFieldIdentifier(message.AsSpan(offset, 2)))
                {
                    fields.Add(new(offset, end - offset, "Unrecognized data", "Expected a field identifier: a letter followed by a letter or digit.", message[offset..end],
                        State: SipLogValueState.Invalid));
                }
                else
                {
                    var identifier = message.Substring(offset, 2);
                    var value = message[(offset + 2)..end];
                    fields.Add(ReadVariableDetail(message[..2], identifier, value, offset));
                }
            }
            offset = end < message.Length && message[end] == '|' ? end + 1 : end;
        }
        return new(name, fields);
    }

    private static bool IsFieldIdentifier(ReadOnlySpan<char> identifier) => identifier.Length == 2 && char.IsAsciiLetterUpper(identifier[0]) &&
        (char.IsAsciiLetterUpper(identifier[1]) || char.IsAsciiDigit(identifier[1]));

    private static int VariableFieldEnd(ReadOnlySpan<char> message, int offset)
    {
        var delimiter = message[offset..].IndexOf('|');
        var end = delimiter < 0 ? message.Length : offset + delimiter;
        // AY's single sequence digit runs directly into AZ, without a '|'.
        return message[offset..].StartsWith("AY", StringComparison.Ordinal) && offset + 5 <= end && message.Slice(offset + 3, 2).SequenceEqual("AZ")
            ? offset + 3 : end;
    }

    private static SipLogDetail ReadVariableDetail(string messageId, string identifier, string value, int offset)
    {
        var detail = ReadPolarisDetail(messageId, identifier, value, offset) ?? SipFixedFields.ReadIdentifiedDetail(identifier, value, offset, messageId);
        if (detail != null)
        {
            return detail;
        }
        var reference = descriptions.GetValueOrDefault(identifier,
            "Unrecognized field\nNo built-in definition; this may be a server-specific extension.");
        var parts = reference.Split('\n', 2);
        return new(offset, value.Length + 2, identifier + " · " + parts[0], parts.Length > 1 ? parts[1] : "", value);
    }

    private static string VariableHelp(SipLogDetail detail)
    {
        var lines = new List<string> { detail.Name.Replace(" · ", " — ", StringComparison.Ordinal), detail.Description, detail.Meaning };
        if (detail.Children != null)
        {
            lines.AddRange(detail.Children.Select(child => child.Name + ": " + child.DisplayValue.Replace("\n", " — ", StringComparison.Ordinal)));
        }
        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }
}
