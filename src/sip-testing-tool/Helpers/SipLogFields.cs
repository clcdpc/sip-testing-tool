namespace Clc.Sip.Helpers;

public sealed record SipLogFlagGroup(int Offset, string Name, string Value, int DefinedCount, char ClearValue = ' ')
{
    public bool IsSet(int index) => index < DefinedCount && Value[index] == 'Y';
    public bool IsValid(int index) => Value[index] == ClearValue || IsSet(index);
}

public sealed record SipLogField(int Offset, string Identifier, string Description, int Length = 2, bool IsFixed = false, SipLogFlagGroup FlagGroup = null);

public static partial class SipLogFields
{
    // SIP2 v2.00, field identifiers and message layouts:
    // https://developers.exlibrisgroup.com/wp-content/uploads/2020/01/3M-Standard-Interchange-Protocol-Version-2.00.pdf
    private static readonly Dictionary<string, string> descriptions = new(StringComparer.Ordinal)
    {
        ["AA"] = "Patron identifier\nThe patron's barcode or other library identifier.",
        ["AB"] = "Item identifier\nThe item's barcode or other library identifier.",
        ["AC"] = "Terminal password\nPassword for the self-check terminal, separate from the login and patron passwords.",
        ["AD"] = "Patron password\nThe patron's PIN or password.",
        ["AE"] = "Personal name\nThe patron's name as supplied by the library system.",
        ["AF"] = "Screen message\nText from the library system to display to the user.",
        ["AG"] = "Print line\nText from the library system to print on a receipt.",
        ["AH"] = "Due date\nWhen the checked-out item is due back.",
        ["AJ"] = "Title identifier\nA title or bibliographic identifier, or the item's title text.",
        ["AL"] = "Blocked card message\nThe reason supplied when blocking a patron's card.",
        ["AM"] = "Library name\nThe library's display name.",
        ["AN"] = "Terminal location\nThe self-check terminal's location supplied by the library system.",
        ["AO"] = "Institution ID\nIdentifies the library or institution for this transaction.",
        ["AP"] = "Current location\nThe item's current location or the location handling the transaction.",
        ["AQ"] = "Permanent location\nThe item's normal home or shelving location.",
        ["AS"] = "Hold items\nAn entry in the patron's list of holds; may repeat for multiple items.",
        ["AT"] = "Overdue items\nAn entry in the patron's list of overdue items.",
        ["AU"] = "Charged items\nAn entry in the patron's list of checked-out items.",
        ["AV"] = "Fine items\nAn entry describing an item associated with a patron fine.",
        ["AY"] = "Sequence number\nA digit from 0 to 9 used to match messages for SIP error detection.",
        ["AZ"] = "Checksum\nFour hexadecimal characters used to detect transmission errors.",
        ["BD"] = "Home address\nThe patron's postal address.",
        ["BE"] = "Email address\nThe patron's email address.",
        ["BF"] = "Home phone number\nThe patron's telephone number.",
        ["BG"] = "Owner\nThe institution that owns the item.",
        ["BH"] = "Currency type\nThe three-letter currency code for a monetary amount, such as USD.",
        ["BI"] = "Cancel\nY cancels the earlier transaction; N does not request cancellation.",
        ["BK"] = "Transaction ID\nAn identifier assigned by the library system or payment device for auditing.",
        ["BL"] = "Valid patron\nY means the library system recognizes the patron; N means it does not.",
        ["BM"] = "Renewed items\nAn entry identifying an item successfully renewed by Renew all.",
        ["BN"] = "Unrenewed items\nAn entry for an item that Renew all could not renew; may include a reason.",
        ["BO"] = "Fee acknowledged\nY accepts the fee associated with the requested transaction.",
        ["BP"] = "Start item\nThe first list entry requested by Patron information, counting from 1.",
        ["BQ"] = "End item\nThe last list entry requested by Patron information, counting from 1.",
        ["BR"] = "Queue position\nThe patron's position in the hold queue.",
        ["BS"] = "Pickup location\nThe location where a hold should be collected.",
        ["BT"] = "Fee type\nA code identifying the kind of fee associated with the transaction.",
        ["BU"] = "Recall items\nAn entry in the patron's list of recalled items.",
        ["BV"] = "Fee amount\nThe amount paid, owed, or charged, depending on the message.",
        ["BW"] = "Expiration date\nThe date and time when the hold expires.",
        ["BX"] = "Supported messages\nEach Y/N position indicates whether a SIP command/response pair is supported.",
        ["BY"] = "Hold type\nThe type of hold, such as a hold on a title or a specific copy.",
        ["BZ"] = "Hold items limit\nThe maximum number of holds allowed for the patron.",
        ["CA"] = "Overdue items limit\nThe patron's permitted overdue-item limit.",
        ["CB"] = "Charged items limit\nThe maximum number of items the patron may have checked out.",
        ["CC"] = "Fee limit\nThe patron's permitted outstanding-fee limit.",
        ["CD"] = "Unavailable hold items\nAn entry for a patron hold that is not yet available.",
        ["CF"] = "Hold queue length\nThe number of holds waiting for the item.",
        ["CG"] = "Fee identifier\nIdentifies the particular fee to which a payment should be applied.",
        ["CH"] = "Item properties\nAdditional item information whose format is agreed with the library system.",
        ["CI"] = "Security inhibit\nY tells the terminal to leave the item's security state unchanged.",
        ["CJ"] = "Recall date\nThe date and time when the recall was issued.",
        ["CK"] = "Media type\nA code identifying the item's material or media format.",
        ["CL"] = "Sort bin\nThe sorting destination assigned to a returned item.",
        ["CM"] = "Hold pickup date\nThe date by which an available hold should be collected.",
        ["CN"] = "Login user ID\nThe account used to log in to the SIP server.",
        ["CO"] = "Login password\nThe password for the SIP login account.",
        ["CP"] = "Location code\nThe self-check location supplied during login.",
        ["CQ"] = "Valid patron password\nY means the supplied patron PIN or password is valid; N means it is not.",
        ["DE"] = "Notice comment (extension)\nComment sent by this tool's server-specific Notice update command.",
        ["VP"] = "Vendor profile (extension)\nThe server-specific vendor profile supplied during login."
    };

    public static SipLogField Find(string transcript, int characterIndex)
    {
        if (string.IsNullOrEmpty(transcript) || characterIndex < 0 || characterIndex >= transcript.Length)
        {
            return null;
        }

        var lineStart = transcript.LastIndexOf('\n', characterIndex) + 1;
        var lineEnd = transcript.IndexOf('\n', characterIndex);
        if (lineEnd < 0)
        {
            lineEnd = transcript.Length;
        }
        if (lineEnd < lineStart)
        {
            return null;
        }

        var line = transcript.AsSpan(lineStart, lineEnd - lineStart).TrimEnd('\r');
        if (!line.StartsWith("--> ", StringComparison.Ordinal) && !line.StartsWith("<-- ", StringComparison.Ordinal))
        {
            return null;
        }

        var message = line[4..];
        if (message.Length < 2 || !char.IsAsciiDigit(message[0]) || !char.IsAsciiDigit(message[1]))
        {
            return null;
        }

        var index = characterIndex - lineStart - 4;
        var fixedField = SipFixedFields.Find(message, index, lineStart + 4, out var fieldStart);
        if (fixedField != null)
        {
            return fixedField;
        }
        if (fieldStart < 0)
        {
            // An unknown message's fixed header is ambiguous. Only recognize
            // identifiers after a delimiter instead of guessing inside its data.
            var delimiter = message.IndexOf('|');
            fieldStart = delimiter < 0 ? message.Length : delimiter + 1;
        }

        while (fieldStart + 2 <= message.Length && fieldStart <= index)
        {
            var identifiedFixedField = SipFixedFields.FindIdentified(message, fieldStart, index, lineStart + 4);
            if (identifiedFixedField != null)
            {
                return identifiedFixedField;
            }
            if (index < fieldStart + 2 && char.IsAsciiLetterUpper(message[fieldStart]) && char.IsAsciiLetterUpper(message[fieldStart + 1]))
            {
                var identifier = message.Slice(fieldStart, 2).ToString();
                var description = descriptions.GetValueOrDefault(identifier,
                    "Unrecognized field\nThis code is not in the built-in SIP2 reference; it may be a server-specific extension.");
                return new SipLogField(lineStart + 4 + fieldStart, identifier, identifier + " — " + description);
            }

            // AY's single sequence digit runs directly into AZ, without a '|'.
            if (message[fieldStart..].StartsWith("AY", StringComparison.Ordinal) && fieldStart + 5 <= message.Length &&
                message.Slice(fieldStart + 3, 2).SequenceEqual("AZ"))
            {
                fieldStart += 3;
                continue;
            }

            var delimiter = message[fieldStart..].IndexOf('|');
            if (delimiter < 0)
            {
                break;
            }
            fieldStart += delimiter + 1;
        }
        return null;
    }

}
