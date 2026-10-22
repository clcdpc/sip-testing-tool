using System.Text.RegularExpressions;

namespace Clc.Sip.Helpers;

public static partial class SipLogFields
{
    // Sources supplied with this change:
    // - SIP Message Documentation (July 2010), pp. 2-4.
    // - RPA via SIP for Vendors 2012, Polaris extensions and AV Field Format table.
    // The references differ on COMPRISE/3M profiles, so identify AV layouts from
    // their value syntax rather than assuming a format from a profile alone.
    private static SipLogDetail ReadPolarisDetail(string messageId, string identifier, string value, int offset)
    {
        if (identifier == "AV" && messageId == "64")
        {
            return ReadFineEntry(value, offset);
        }
        if (identifier == "CV" && messageId == "93")
        {
            return new(offset, value.Length + 2, "CV · Vendor profile (legacy Polaris)", "Polaris 3.6 used CV at login; later versions use VP.", value);
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
