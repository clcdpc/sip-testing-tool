using System.Globalization;

namespace Clc.Sip.Helpers;

internal static class SipFixedFields
{
    private sealed record Definition(int Length, string Name, string Description, Func<string, string> Decode = null, string[] FlagNames = null);
    private sealed record Layout(string Name, params Definition[] Fields)
    {
        public int HeaderLength { get; } = 2 + Fields.Sum(field => field.Length);
    }

    private static readonly Definition transactionDate = new(18, "Transaction date/time", "When this SIP transaction took place. Format: YYYYMMDDZZZZHHMMSS.", DateValue);
    private static readonly Definition dueDate = new(18, "No-block due date/time", "Due date assigned during offline operation. Eighteen spaces indicate no date was supplied.", DateValue);
    private static readonly Definition returnDate = new(18, "Return date/time", "When the item was returned, which may differ from the transaction time.", DateValue);
    private static readonly Definition language = new(3, "Language", "Language requested for screen messages and printed text.", value => Code(value,
        ["Unspecified", "English", "French", "German", "Italian", "Dutch", "Swedish", "Finnish", "Spanish", "Danish", "Portuguese", "Canadian French", "Norwegian", "Hebrew", "Japanese", "Russian", "Arabic", "Polish", "Greek", "Chinese", "Korean", "North American Spanish", "Tamil", "Malay", "United Kingdom", "Icelandic", "Belgian", "Taiwanese"]));
    private static readonly Definition noBlock = Flag("No block", "Whether this reports a transaction already performed while the server was offline.", "Already completed offline; do not block the transaction.", "Normal online transaction.");
    private static readonly Definition renewalPolicy = Flag("SC renewal policy", "Whether this terminal is configured to allow renewals.", "Renewals allowed.", "Renewals not allowed.");
    private static readonly Definition thirdParty = Flag("Third-party renewals allowed", "Whether someone other than the original borrower may renew the item.", "Third-party renewals allowed.", "Third-party renewals not allowed.");
    private static readonly Definition ok = new(1, "OK", "Whether the requested action completed successfully. 1 = success; 0 = failure.", value => value switch { "1" => "Action completed successfully.", "0" => "Action failed or was refused.", _ => "Unrecognized result code." });
    private static readonly Definition renewalOk = Flag("Renewal OK", "Whether this checkout is a renewal for the same patron.", "The item was already checked out to this patron.", "The item was not already checked out to this patron.");
    private static readonly Definition magneticMedia = Flag("Magnetic media", "Whether special handling is needed for magnetic media.", "Item is magnetic media.", "Item is not magnetic media.", "The server does not identify magnetic media.");
    private static readonly Definition desensitize = Flag("Desensitize", "Whether the terminal should deactivate the item's security marker.", "Deactivate the security marker.", "Leave the security marker active.", "Security action unknown; treated as N.");
    private static readonly Definition resensitize = Flag("Resensitize", "Whether the terminal should reactivate the returned item's security marker.", "Reactivate the security marker.", "Do not reactivate the security marker.");
    private static readonly Definition alert = Flag("Alert", "Whether this return needs staff attention or an audible alert.", "Alert requested.", "No alert requested.");
    private static readonly Definition patronStatus = new(14, "Patron status", "Y sets a restriction or condition; a space means it is not set.", FlagNames:
        ["Borrowing privileges denied", "Renewal privileges denied", "Recall privileges denied", "Hold privileges denied", "Card reported lost", "Too many items checked out", "Too many overdue items", "Too many renewals", "Too many claims of items returned", "Too many lost items", "Excessive outstanding fines", "Excessive outstanding fees", "Overdue recall", "Too many billed items"]);
    private static readonly Definition summary = new(10, "Summary", "Y requests detailed items; a space requests counts only. The last four positions are reserved.", FlagNames:
        ["Hold items", "Overdue items", "Checked-out items", "Fine items", "Recall items", "Unavailable holds"]);
    private static readonly string[] supportedMessageNames =
        ["Patron status request", "Checkout", "Checkin", "Block patron", "SC/ACS status", "SC/ACS resend", "Login", "Patron information", "End patron session", "Fee paid", "Item information", "Item status update", "Patron enable", "Hold", "Renew", "Renew all"];
    private static readonly Definition feeType = new(2, "Fee type", "The type of fee being paid or charged.", value => Code(value,
        ["Other / unknown", "Administrative", "Damage", "Overdue", "Processing", "Rental", "Replacement", "Computer access", "Hold fee"], 1));
    private static readonly Definition paymentType = new(2, "Payment type", "How the patron paid the fee.", value => Code(value, ["Cash", "VISA", "Credit card"]));
    private static readonly Definition currency = new(3, "Currency type", "Three-letter currency code for this payment, such as USD.");
    private static readonly Definition protocolVersion = new(4, "Protocol version", "SIP protocol version in x.xx format, such as 2.00.");
    private static readonly Definition uidAlgorithm = new(1, "UID algorithm", "Encoding agreed with the server for the login user ID. The tool sends the entered value without encrypting it.", AlgorithmValue);
    private static readonly Definition pwdAlgorithm = new(1, "PWD algorithm", "Encoding agreed with the server for the login password. The tool sends the entered value without encrypting it.", AlgorithmValue);
    private static readonly Definition holdMode = new(1, "Hold mode", "The operation requested on the patron's hold.", value => value switch { "+" => "Add a hold.", "-" => "Delete a hold.", "*" => "Change a hold.", _ => "Unrecognized hold mode." });

    // Required fixed fields only. Identified optional fields are parsed separately
    // by SipLogFields, even when their value has a fixed width (for example BH).
    private static readonly Dictionary<string, Layout> layouts = new(StringComparer.Ordinal)
    {
        ["01"] = new("Block patron", Flag("Card retained", "Whether the terminal retained the patron's library card.", "Card retained.", "Card not retained."), transactionDate),
        ["09"] = new("Checkin", noBlock, transactionDate, returnDate),
        ["10"] = new("Checkin response", ok, resensitize, magneticMedia, alert, transactionDate),
        ["11"] = new("Checkout", renewalPolicy, noBlock, transactionDate, dueDate),
        ["12"] = new("Checkout response", ok, renewalOk, magneticMedia, desensitize, transactionDate),
        ["15"] = new("Hold", holdMode, transactionDate),
        ["16"] = new("Hold response", ok, Flag("Available", "Whether the requested item is available.", "Item available.", "Item unavailable."), transactionDate),
        ["17"] = new("Item information", transactionDate),
        ["18"] = new("Item information response",
            new(2, "Circulation status", "The item's current circulation state.", value => Code(value,
                ["Other", "On order", "Available", "Checked out", "Checked out; recall restricted", "In process", "Recalled", "On hold shelf", "Waiting to be shelved", "In transit", "Claimed returned", "Lost", "Missing"], 1)),
            new(2, "Security marker", "The type of security marker attached to the item.", value => Code(value, ["Other", "None", "3M Tattle-Tape", "3M Whisper Tape"])), feeType, transactionDate),
        ["19"] = new("Item status update", transactionDate),
        ["20"] = new("Item status update response", new(1, "Item properties OK", "1 means the server stored the item properties; other values mean it did not."), transactionDate),
        ["23"] = new("Patron status request", language, transactionDate),
        ["24"] = new("Patron status response", patronStatus, language, transactionDate),
        ["25"] = new("Patron enable", transactionDate),
        ["26"] = new("Patron enable response", patronStatus, language, transactionDate),
        ["29"] = new("Renew", thirdParty, noBlock, transactionDate, dueDate),
        ["30"] = new("Renew response", ok, renewalOk, magneticMedia, desensitize, transactionDate),
        ["35"] = new("End patron session", transactionDate),
        ["36"] = new("End session response", Flag("End session", "Whether the server ended the patron's session.", "Session ended.", "Session could not be ended."), transactionDate),
        ["37"] = new("Fee paid", transactionDate, feeType, paymentType, currency),
        ["38"] = new("Fee paid response", Flag("Payment accepted", "Whether the server accepted this payment.", "Payment accepted.", "Payment refused."), transactionDate),
        ["43"] = new("Notice update (extension)",
            new(2, "Delivery status", "Delivery result in this tool's server-specific Notice update command.", value => Code(value, ["Unsuccessful delivery", "Successful delivery", "Intercepted / rejected"])),
            transactionDate, new(18, "Delivery date/time", "When the notice was delivered or delivery was attempted.", DateValue),
            new(3, "Delivery medium", "How this server-specific notice was delivered.", value => Code(value, ["Any / unknown", "Mail", "Phone", "Fax", "Email", "SMS"])),
            new(3, "Notice type", "The category of this server-specific notice.", value => Code(value, ["Any / unknown", "Overdue", "Recall", "Hold", "Fine / billing"]))),
        ["63"] = new("Patron information", language, transactionDate, summary),
        ["64"] = new("Patron information response", patronStatus, language, transactionDate,
            Count("Hold items count"), Count("Overdue items count"), Count("Charged items count"), Count("Fine items count"), Count("Recall items count"), Count("Unavailable holds count")),
        ["65"] = new("Renew all", transactionDate),
        ["66"] = new("Renew all response", ok, Count("Renewed count"), Count("Unrenewed count"), transactionDate),
        ["93"] = new("Login", uidAlgorithm, pwdAlgorithm),
        ["94"] = new("Login response", new Definition(1, "Login OK", "Whether the server accepted the login. 1 = accepted; 0 = rejected.", value => value switch { "1" => "Login accepted.", "0" => "Login rejected.", _ => "Unrecognized login result." })),
        ["96"] = new("Request SC resend"),
        ["97"] = new("Request ACS resend"),
        ["98"] = new("ACS status",
            Flag("Online status", "Whether the library system is online.", "Server online.", "Server offline."),
            Flag("Checkin OK", "Whether the terminal may check in items.", "Checkin allowed.", "Checkin not allowed."),
            Flag("Checkout OK", "Whether the terminal may check out items.", "Checkout allowed.", "Checkout not allowed."),
            Flag("ACS renewal policy", "Whether the server permits the terminal to process renewals.", "Renewals allowed.", "Renewals not allowed."),
            Flag("Status update OK", "Whether the terminal may update patron status.", "Status updates allowed.", "Status updates not allowed."),
            Flag("Offline OK", "Whether the server supports the terminal's offline transactions.", "Offline operation supported.", "Offline operation not supported."),
            new(3, "Timeout period", "Wait before aborting a transaction, in tenths of a second. 000 = offline; 999 = unknown.", value => value switch
            {
                "000" => "Server offline.",
                "999" => "Timeout unknown.",
                _ => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var tenths) ? (tenths / 10m).ToString("0.0", CultureInfo.InvariantCulture) + " seconds." : "Unrecognized timeout."
            }),
            new(3, "Retries allowed", "How many times a transaction may be retried. 999 means unknown."),
            new(18, "Date/time sync", "Server time for clock synchronization. All zeroes mean this is unsupported.", DateValue), protocolVersion),
        ["99"] = new("SC status", new(1, "Status code", "The self-check terminal's current status.", value => Code(value, ["Terminal OK", "Printer out of paper", "Terminal shutting down"])),
            new(3, "Maximum print width", "The maximum number of characters the terminal's printer can print per line."), protocolVersion)
    };

    // These optional fields carry identifiers but their values still have fixed
    // widths. AY and AZ are the two fields whose terminators are omitted.
    private static readonly Dictionary<string, Definition> identifiedFields = new(StringComparer.Ordinal)
    {
        ["AY"] = new(1, "Sequence number", "A digit from 0 to 9 used to match messages for SIP error detection.", value => char.IsAsciiDigit(value[0]) ? "Sequence " + value + "." : "Unrecognized sequence digit."),
        ["AZ"] = new(4, "Checksum", "Four hexadecimal digits used to detect transmission errors.", value => value.All(Uri.IsHexDigit) ? "Hexadecimal checksum: " + value + "." : "Unrecognized checksum; expected four hexadecimal digits."),
        ["BH"] = currency,
        ["BI"] = Flag("Cancel", "Whether this cancels the earlier transaction.", "Cancellation requested.", "No cancellation requested."),
        ["BL"] = Flag("Valid patron", "Whether the library system recognizes the patron.", "Patron recognized.", "Patron not recognized."),
        ["BO"] = Flag("Fee acknowledged", "Whether the patron accepts the transaction's fee.", "Fee acknowledged.", "Fee not acknowledged."),
        ["BT"] = feeType,
        ["BW"] = new(18, "Expiration date/time", "When the hold expires.", DateValue),
        ["BY"] = new(1, "Hold type", "Whether the hold applies to a title or a particular copy.", value => Code(value, ["Other", "Any copy of the title", "A specific copy", "Any copy at one branch or location"], 1)),
        ["BZ"] = new(4, "Hold items limit", "Maximum number of holds allowed for this patron."),
        ["CA"] = new(4, "Overdue items limit", "The patron's permitted overdue-item limit."),
        ["CB"] = new(4, "Charged items limit", "Maximum number of items the patron may have checked out."),
        ["CI"] = Flag("Security inhibit", "Whether the terminal should leave the item's security state unchanged.", "Leave the security state unchanged.", "Normal security handling."),
        ["CJ"] = new(18, "Recall date/time", "When the recall was issued.", DateValue),
        ["CK"] = new(3, "Media type", "The item's material or media format.", value => Code(value, ["Other", "Book", "Magazine", "Bound journal", "Audio tape", "Video tape", "CD / CD-ROM", "Diskette", "Book with diskette", "Book with CD", "Book with audio tape"])),
        ["CM"] = new(18, "Hold pickup date/time", "The date by which an available hold should be collected.", DateValue),
        ["CQ"] = Flag("Valid patron password", "Whether the supplied patron PIN or password is valid.", "Patron password valid.", "Patron password invalid.")
    };

    public static SipLogField Find(ReadOnlySpan<char> message, int index, int messageOffset, out int firstVariableOffset)
    {
        firstVariableOffset = -1;
        if (!layouts.TryGetValue(message[..2].ToString(), out var layout))
        {
            return null;
        }
        firstVariableOffset = layout.HeaderLength;
        if (index is >= 0 and < 2)
        {
            return new SipLogField(messageOffset, "Message ID", "Message ID — " + message[..2].ToString() + "\n" + layout.Name, 2, true);
        }

        var offset = 2;
        foreach (var definition in layout.Fields)
        {
            if (index >= offset && index < offset + definition.Length)
            {
                if (offset + definition.Length > message.Length || message.Slice(offset, definition.Length).Contains('|'))
                {
                    return null; // Do not outline a partial or delimited fixed field.
                }
                var value = message.Slice(offset, definition.Length).ToString();
                if (definition.FlagNames != null)
                {
                    return DescribeFlag(definition.Name, definition.Description, definition.FlagNames, value, index - offset, messageOffset, offset);
                }
                var description = Describe(definition, value, offset, definition.Length);
                return new SipLogField(messageOffset + offset, definition.Name, description, definition.Length, true);
            }
            offset += definition.Length;
        }
        return null;
    }

    public static SipLogField FindIdentified(ReadOnlySpan<char> message, int fieldStart, int index, int messageOffset)
    {
        var identifier = message.Slice(fieldStart, 2).ToString();
        if (identifier == "BX" && index >= fieldStart + 2)
        {
            var end = message[fieldStart..].IndexOf('|');
            var flagValues = message.Slice(fieldStart + 2, (end < 0 ? message.Length - fieldStart : end) - 2);
            if (flagValues.Length >= supportedMessageNames.Length && index < fieldStart + 2 + flagValues.Length)
            {
                return DescribeFlag("Supported messages (BX)", "Y means supported; N means not supported.", supportedMessageNames, flagValues.ToString(),
                    index - fieldStart - 2, messageOffset, fieldStart + 2, 'N', reservedRemainder: false);
            }
        }
        if (index >= fieldStart + 20 || !identifiedFields.TryGetValue(identifier, out var definition))
        {
            return null;
        }
        var delimiter = message[fieldStart..].IndexOf('|');
        var fieldEnd = delimiter < 0 ? message.Length : fieldStart + delimiter;
        if (identifier == "AY" && fieldStart + 5 <= message.Length && message.Slice(fieldStart + 3, 2).SequenceEqual("AZ"))
        {
            fieldEnd = fieldStart + 3;
        }
        var length = definition.Length + 2;
        if (fieldEnd - fieldStart != length || index >= fieldEnd)
        {
            return null;
        }
        var value = message.Slice(fieldStart + 2, definition.Length).ToString();
        return new SipLogField(messageOffset + fieldStart, identifier, identifier + " — " + Describe(definition, value, fieldStart, length), length, true);
    }

    private static string Describe(Definition definition, string value, int offset, int length)
    {
        var rawValue = string.IsNullOrWhiteSpace(value) ? $"({value.Length} spaces)" : '"' + value.Replace(' ', '·') + '"';
        var decoded = definition.Decode?.Invoke(value);
        var unit = definition.Length == 1 ? "value character" : "value characters";
        return $"{definition.Name} — fixed field ({definition.Length} {unit})\n" + definition.Description +
            $"\nValue: {rawValue}" + (decoded == null ? "" : "\n" + decoded) + $"\nMessage characters {offset + 1}–{offset + length}.";
    }

    private static Definition Flag(string name, string description, string yes, string no, string unknown = null) =>
        new(1, name, description, value => value switch { "Y" => yes, "N" => no, "U" when unknown != null => unknown, _ => "Unrecognized flag; expected Y or N" + (unknown == null ? "." : " or U.") });

    private static Definition Count(string name) => new(4, name, "Number of items in this category. Four spaces mean the count is unavailable.", value =>
        string.IsNullOrWhiteSpace(value) ? "Count unavailable." : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " item." : " items.") : "Unrecognized count.");

    private static string Code(string value, string[] labels, int firstCode = 0) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var code) && code >= firstCode && code - firstCode < labels.Length
            ? labels[code - firstCode] + "." : "Unrecognized or server-specific code.";

    private static string AlgorithmValue(string value) => value == "0" ? "Plain text; no encryption." : "Server-specific encoding identifier; no encryption is performed by the tool.";

    private static string DateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No date supplied.";
        }
        if (value == new string('0', 18))
        {
            return "All-zero date/time; no usable date supplied.";
        }
        if (!DateTime.TryParseExact(value[..8] + value[12..], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return "Unrecognized date/time; expected YYYYMMDDZZZZHHMMSS.";
        }
        var zone = value.Substring(8, 4);
        return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
            (zone == "    " ? " (local time)." : zone == "   Z" ? " (UTC)." : $" (SIP time-zone field: {zone.Replace(' ', '·')}).");
    }

    private static SipLogField DescribeFlag(string name, string description, string[] labels, string value, int flagIndex,
        int messageOffset, int groupOffset, char clearValue = ' ', bool reservedRemainder = true)
    {
        var group = new SipLogFlagGroup(messageOffset + groupOffset, name, value, labels.Length, clearValue);
        string Label(int index) => index < labels.Length ? labels[index] : reservedRemainder ? "Reserved position" : "Unrecognized position (extension)";
        string State(int index)
        {
            if (group.IsSet(index))
            {
                return "SET (Y)";
            }
            if (value[index] == clearValue)
            {
                return index >= labels.Length && reservedRemainder ? "Reserved; blank as expected" : clearValue == ' ' ? "Not set (space)" : "Not set (N)";
            }
            return index >= labels.Length && reservedRemainder ? $"Unexpected value '{value[index]}' in a reserved position" : $"Unrecognized flag value '{value[index]}'";
        }
        var checklist = Enumerable.Range(0, value.Length).Select(index =>
            $"{(index == flagIndex ? ">" : " ")} {index + 1:00}. {Label(index)}: {State(index)}");
        var activeCount = Enumerable.Range(0, labels.Length).Count(group.IsSet);
        var help = $"{name} — flag {flagIndex + 1} of {value.Length} (SIP position {flagIndex})\n" +
            $"{Label(flagIndex)}\nState: {State(flagIndex)}.\n{description}\nSet flags: {activeCount} of {labels.Length}.\n\n" +
            string.Join("\n", checklist) + $"\n\nMessage character {groupOffset + flagIndex + 1}.";
        return new SipLogField(group.Offset + flagIndex, name, help, 1, true, group);
    }
}
