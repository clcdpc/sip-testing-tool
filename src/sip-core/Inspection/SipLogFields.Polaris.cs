using System.Text.RegularExpressions;

namespace Clc.Sip.Helpers;

public static partial class SipLogFields
{
    // Sources supplied with this change:
    // - SIP Message Documentation (July 2010), pp. 2-4.
    // - RPA via SIP for Vendors 2012, Polaris extensions and AV Field Format table.
    // - Polaris_SIP2_Custom_Extensions_8.1_Updated.pdf, revised 23 October 2026,
    //   sections 2-5 (pp. 2-5), for SIPService.exe 8.1.7065.1. This is an unofficial,
    //   binary-derived reference: monetary units, processing gates and unproven
    //   business meanings remain qualified rather than inferred from tag names.
    // The historical references differ on COMPRISE/3M profiles, so identify AV layouts from
    // their value syntax rather than assuming a format from a profile alone.
    private static SipLogDetail ReadPolarisDetail(string messageId, string identifier, string value, int offset)
    {
        if (messageId is "24" or "64")
        {
            var patronField = identifier switch
            {
                "FA" => Field("Accrued fines total", "The analyzed Polaris 8.1 build truncates the accrued fines total to a 32-bit integer and sends decimal text. Monetary units have not been confirmed."),
                "PC" => Field("Internal patron ID", "The internal Polaris PatronID, serialized as decimal text."),
                "PB" => Field("Resolved patron barcode", "The barcode resolved from the patron record, including when authentication used a username."),
                _ => null
            };
            if (patronField != null) { return patronField; }
        }
        if (identifier == "DB" && messageId is "42" or "64")
        {
            return Field("Patron credit balance", "The patron's CreditBalance rendered as signed integer text. Monetary units have not been confirmed. " +
                (messageId == "64" ? "Emitted by the EnvisionWare processing branch." : "Available when the deposit handler has refreshed patron data; availability is not guaranteed."));
        }
        if (identifier == "SI" && messageId == "64")
        {
            return Field("Internal patron ID", "PatronID emitted by the EnvisionWare processing branch. It matches PC in the analyzed path; the reason for the duplicate identifier is unconfirmed.");
        }
        if (identifier == "TX" && messageId == "42")
        {
            return Field("Deposit response value", "The analyzed Polaris 8.1.7065.1 build emits literal decimal 0. The field's broader business meaning has not been established.");
        }
        if (identifier == "AA" && messageId == "42")
        {
            return new(offset, value.Length + 2, "AA · Patron identifier", "Copied from the deposit-account activity request (41).", value);
        }
        if (identifier == "BK" && messageId is "43" or "44")
        {
            return Field(messageId == "44" ? "Echoed request value" : "Notice request value",
                "Polaris copies BK from request 43 into response 44 unchanged. Its business meaning is unconfirmed; the response does not generate a notification ID.");
        }
        if (identifier == "AV" && messageId == "64")
        {
            return ReadFineEntry(value, offset);
        }
        if (identifier == "CV" && messageId == "93")
        {
            return new(offset, value.Length + 2, "CV · Vendor profile (legacy Polaris)", "Polaris 3.6 used CV at login; later versions use VP. The 8.1 reference does not establish CV as a login alias.", value);
        }
        if (identifier == "CP" && messageId == "93" && value is "AUTHENTICATE" or "AUTHENTICATE_INSENSITIVE")
        {
            return new(offset, value.Length + 2, "CP · Location code", "Polaris authentication override.", value,
                value == "AUTHENTICATE" ? "Require case-sensitive patron PIN/password authentication." :
                "Require case-insensitive patron PIN/password authentication.");
        }
        if (identifier == "AA" && messageId is "63" or "64")
        {
            return new(offset, value.Length + 2, "AA · Patron identifier", messageId == "63" ?
                "Patron barcode; Polaris also accepts a patron username in this request." :
                "Patron barcode. Polaris returns the barcode even when the request used a username.", value);
        }
        return null;

        SipLogDetail Field(string name, string description) => new(offset, value.Length + 2, identifier + " · " + name + " (Polaris)", description, value);
    }

    private static SipLogDetail ReadFineEntry(string value, int offset)
    {
        var match = FineTransaction().Match(value);
        var children = new List<SipLogDetail>();
        string format;
        if (match.Success)
        {
            AddPart(match.Groups["id"], "Fine transaction ID");
            AddPart(match.Groups["amount"], "Outstanding amount", "Unpaid balance for this entry.");
            format = "Polaris default format.";
            var extra = match.Groups["extra"];
            if (extra.Success && extra.Length > 0)
            {
                var details = FineItemDetails().Match(extra.Value);
                if (details.Success)
                {
                    format = "Polaris detailed format.";
                    AddPart(details.Groups["type"], "Fine type", relativeOffset: extra.Index);
                    AddPart(details.Groups["description"], "Fee description", relativeOffset: extra.Index);
                    AddPart(details.Groups["barcode"], "Related item barcode", relativeOffset: extra.Index);
                    AddPart(details.Groups["title"], "Related item title", "May be truncated to 80 characters by Polaris.", extra.Index);
                }
                else
                {
                    AddPart(extra, "Additional fine data", "Does not match a documented Polaris detail layout.");
                }
            }
        }
        else
        {
            match = FineAmountFirst().Match(value);
            if (!match.Success)
            {
                return null; // Keep other servers' AV formats intact.
            }
            format = "Polaris 3M post-4.1 format.";
            AddPart(match.Groups["amount"], "Outstanding amount", "Unpaid balance for this entry.");
            AddPart(match.Groups["type"], "Fine type");
            // The reference specifies a combined description/title without a
            // delimiter between them. Keep that text together.
            AddPart(match.Groups["description"], "Fee description / title");
        }
        return new(offset, value.Length + 2, "AV · Fine item details", format, value, Children: children);

        void AddPart(Group part, string name, string description = "", int relativeOffset = 0)
        {
            if (part.Success)
            {
                children.Add(new(offset + 2 + relativeOffset + part.Index, part.Length, name, description, part.Value));
            }
        }
    }

    [GeneratedRegex("""\A(?<id>[0-9]+) +(?<amount>\$-?[0-9]+(?:,[0-9]{3})*(?:\.[0-9]+)?)(?: +(?<extra>.*))?\z""", RegexOptions.CultureInvariant)]
    private static partial Regex FineTransaction();

    [GeneratedRegex("""\A"(?<type>[^" ]+)(?: +(?<description>[^"]*))?" +(?<barcode>[^ ]+)(?: +(?<title>.*))?\z""", RegexOptions.CultureInvariant)]
    private static partial Regex FineItemDetails();

    [GeneratedRegex("""\A(?<amount>\$?-?[0-9]+(?:,[0-9]{3})*\.[0-9]{2}) +(?<type>[^ ]+) +(?<description>.+)\z""", RegexOptions.CultureInvariant)]
    private static partial Regex FineAmountFirst();
}
