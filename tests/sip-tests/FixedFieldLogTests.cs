using Clc.Sip.Helpers;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class FixedFieldLogTests
{
    [TestMethod(DisplayName = "Checkout fixed fields include every date character and blank space")]
    public void FixedFieldRangesCoverExactlyTheirValues()
    {
        const string transcript = "--> 11YN20261021    153000                  AOlibrary|AApatron|ABitem|AY0AZABCD";
        var offset = 4;
        foreach (var (name, value, explanation) in new[]
        {
            ("Message ID", "11", "Checkout"),
            ("SC renewal policy", "Y", "Renewals allowed"),
            ("No block", "N", "Normal online transaction"),
            ("Transaction date/time", "20261021    153000", "2026-10-21 15:30:00 (local time)"),
            ("No-block due date/time", "                  ", "No date supplied")
        })
        {
            for (var index = offset; index < offset + value.Length; index++)
            {
                var field = SipLogFields.Find(transcript, index);
                Assert.IsNotNull(field, name);
                Assert.IsTrue(field.IsFixed);
                Assert.AreEqual(name, field.Identifier);
                Assert.AreEqual(offset, field.Offset);
                Assert.AreEqual(value.Length, field.Length);
                StringAssert.Contains(field.Description, explanation);
                Assert.AreNotEqual(field, SipLogFields.Find(transcript, offset - 1));
                Assert.AreNotEqual(field, SipLogFields.Find(transcript, offset + value.Length));
            }
            offset += value.Length;
        }
        var variable = SipLogFields.Find(transcript, offset);
        Assert.AreEqual("AO", variable?.Identifier);
        Assert.IsFalse(variable.IsFixed);
        Assert.AreEqual(2, variable.Length);
        Assert.IsNull(SipLogFields.Find(transcript, offset + 2), "Variable values must keep their previous behavior.");
    }

    [TestMethod(DisplayName = "Fixed values explain status codes, dates, flags and counts")]
    [DataRow("<-- 121YNU20261021    153000AOlibrary|", 2, "OK", 1, "Action completed successfully")]
    [DataRow("<-- 101YNN20261021    153000AOlibrary|", 5, "Alert", 1, "No alert requested")]
    [DataRow("<-- 1803020420261021    153000ABitem|", 2, "Circulation status", 2, "Available")]
    [DataRow("<-- 1803020420261021    153000ABitem|", 4, "Security marker", 2, "3M Tattle-Tape")]
    [DataRow("--> 3720261021    1530000400USDBV1.50|", 20, "Fee type", 2, "Overdue")]
    [DataRow("--> 3720261021    1530000400USDBV1.50|", 22, "Payment type", 2, "Cash")]
    [DataRow("--> 3720261021    1530000400USDBV1.50|", 24, "Currency type", 3, "USD")]
    [DataRow("--> 2300120261021    153000AOlibrary|", 2, "Language", 3, "English")]
    [DataRow("--> 6300020261021    153000  Y       AOlibrary|", 23, "Summary", 1, "Checked-out items")]
    [DataRow("<-- 64Y             00020261021    153000000100020003000400050006AOlibrary|", 2, "Patron status", 1, "Borrowing privileges denied")]
    [DataRow("<-- 64              00020261021    153000000100020003000400050006AOlibrary|", 37, "Hold items count", 4, "1 item")]
    [DataRow("<-- 64              00020261021    153000000100020003000400050006AOlibrary|", 57, "Unavailable holds count", 4, "6 items")]
    [DataRow("<-- 98YYYYYY03000320261021    1530002.00AOlibrary|", 8, "Timeout period", 3, "3.0 seconds")]
    [DataRow("<-- 98YYYYYY03000320261021    1530002.00AOlibrary|", 14, "Date/time sync", 18, "2026-10-21 15:30:00")]
    [DataRow("--> 9901002.00AY0AZABCD", 3, "Maximum print width", 3, "100")]
    [DataRow("--> 9901002.00AY0AZABCD", 6, "Protocol version", 4, "2.00")]
    [DataRow("--> 9300CNuser|COpassword|", 2, "UID algorithm", 1, "Plain text")]
    [DataRow("--> 9300CNuser|COpassword|", 3, "PWD algorithm", 1, "Plain text")]
    [DataRow("<-- 941AY0AZABCD", 2, "Login OK", 1, "Login accepted")]
    [DataRow("--> 430120261021    15300020261021    150000004003AOlibrary|", 2, "Delivery status", 2, "Successful delivery")]
    [DataRow("--> 430120261021    15300020261021    150000004003AOlibrary|", 40, "Delivery medium", 3, "Email")]
    [DataRow("--> 430120261021    15300020261021    150000004003AOlibrary|", 43, "Notice type", 3, "Hold")]
    public void ValuesHaveMessageSpecificDescriptions(string transcript, int messageOffset, string name, int length, string explanation)
    {
        var field = SipLogFields.Find(transcript, 4 + messageOffset);
        Assert.IsNotNull(field);
        Assert.IsTrue(field.IsFixed);
        Assert.AreEqual(name, field.Identifier);
        Assert.AreEqual(4 + messageOffset, field.Offset);
        Assert.AreEqual(length, field.Length);
        StringAssert.Contains(field.Description, explanation);
    }

    [TestMethod(DisplayName = "Malformed and unknown fixed values remain readable without guessing")]
    public void InvalidAndBlankValuesDoNotBreakHelp()
    {
        var utc = SipLogFields.Find("--> 1720261021   Z153000AOlibrary|", 6);
        StringAssert.Contains(utc.Description, "2026-10-21 15:30:00 (UTC)");
        var invalid = SipLogFields.Find("--> 1720261399    996199AOlibrary|", 6);
        StringAssert.Contains(invalid.Description, "Unrecognized date/time");
        var blankCount = SipLogFields.Find("<-- 661        20261021    153000AOlibrary|", 7);
        Assert.AreEqual("Renewed count", blankCount?.Identifier);
        StringAssert.Contains(blankCount.Description, "Count unavailable");
        Assert.IsNull(SipLogFields.Find("--> 1720261021", 6), "A truncated date must not be outlined as a complete field.");
        Assert.IsNull(SipLogFields.Find("--> 1720261021|AAvalue|", 6), "A delimiter must not become part of a fixed field.");
        Assert.IsNull(SipLogFields.Find("<-- 88unknownheader|AApatron|", 6), "Unknown message headers remain ambiguous.");
        var variable = SipLogFields.Find("<-- 121YNN20261021    153000AOlibrary|BT04|", 4 + 24 + "AOlibrary|".Length);
        Assert.AreEqual("BT", variable?.Identifier);
        Assert.IsTrue(variable.IsFixed, "Optional fields with fixed widths should explain their values too.");
        Assert.AreEqual(4, variable.Length);
    }

    [TestMethod(DisplayName = "Identified fixed fields include their values in help and outlines")]
    [DataRow("AY0", "AY", "Sequence 0")]
    [DataRow("AZABCD", "AZ", "Hexadecimal checksum: ABCD")]
    [DataRow("BHUSD", "BH", "Currency type")]
    [DataRow("BIY", "BI", "Cancellation requested")]
    [DataRow("BLN", "BL", "Patron not recognized")]
    [DataRow("BOY", "BO", "Fee acknowledged")]
    [DataRow("BT04", "BT", "Overdue")]
    [DataRow("BW20261028    153000", "BW", "2026-10-28 15:30:00")]
    [DataRow("BY2", "BY", "Any copy of the title")]
    [DataRow("BZ0005", "BZ", "Hold items limit")]
    [DataRow("CA0002", "CA", "Overdue items limit")]
    [DataRow("CB0010", "CB", "Charged items limit")]
    [DataRow("CIY", "CI", "Leave the security state unchanged")]
    [DataRow("CJ20261028    153000", "CJ", "2026-10-28 15:30:00")]
    [DataRow("CK001", "CK", "Book")]
    [DataRow("CM20261028    153000", "CM", "2026-10-28 15:30:00")]
    [DataRow("CQY", "CQ", "Patron password valid")]
    public void IdentifiedFixedValuesHaveCompleteRanges(string wireField, string identifier, string explanation)
    {
        const string prefix = "<-- 121YNN20261021    153000AOlibrary|AApatron|ABitem|";
        var transcript = prefix + wireField + (identifier is "AY" or "AZ" ? "" : "|");
        for (var index = prefix.Length; index < prefix.Length + wireField.Length; index++)
        {
            var field = SipLogFields.Find(transcript, index);
            Assert.IsNotNull(field);
            Assert.IsTrue(field.IsFixed);
            Assert.AreEqual(identifier, field.Identifier);
            Assert.AreEqual(prefix.Length, field.Offset);
            Assert.AreEqual(wireField.Length, field.Length);
            StringAssert.Contains(field.Description, explanation);
        }
        Assert.IsNull(SipLogFields.Find(transcript, transcript.Length));
        Assert.IsNull(SipLogFields.Find(prefix + "BHUS|", prefix.Length + 2), "Incomplete identified fields must not guess a value range.");
    }

    [TestMethod(DisplayName = "Each patron-status position identifies its own condition and state")]
    [DataRow(0, "Borrowing privileges denied")]
    [DataRow(1, "Renewal privileges denied")]
    [DataRow(2, "Recall privileges denied")]
    [DataRow(3, "Hold privileges denied")]
    [DataRow(4, "Card reported lost")]
    [DataRow(5, "Too many items checked out")]
    [DataRow(6, "Too many overdue items")]
    [DataRow(7, "Too many renewals")]
    [DataRow(8, "Too many claims of items returned")]
    [DataRow(9, "Too many lost items")]
    [DataRow(10, "Excessive outstanding fines")]
    [DataRow(11, "Excessive outstanding fees")]
    [DataRow(12, "Overdue recall")]
    [DataRow(13, "Too many billed items")]
    public void PatronStatusFlagsResolveIndividually(int position, string condition)
    {
        var flags = new string(' ', 14).ToCharArray();
        flags[position] = 'Y';
        foreach (var message in new[] { "24", "26", "64" })
        {
            var transcript = "<-- " + message + new string(flags) + "00020261021    153000" +
                (message == "64" ? "000000000000000000000000" : "") + "AOlibrary|AApatron|";
            var field = SipLogFields.Find(transcript, 6 + position);
            Assert.IsNotNull(field);
            Assert.AreEqual(6 + position, field.Offset);
            Assert.AreEqual(1, field.Length);
            Assert.AreEqual(condition, field.Description.Split('\n')[1]);
            StringAssert.Contains(field.Description, $"flag {position + 1} of 14 (SIP position {position})");
            StringAssert.Contains(field.Description, "State: SET (Y).");
            StringAssert.Contains(field.Description, "Set flags: 1 of 14.");
            StringAssert.Contains(field.Description, $"> {position + 1:00}. {condition}: SET (Y)");
            Assert.AreEqual(6, field.FlagGroup.Offset);
            Assert.AreEqual(new string(flags), field.FlagGroup.Value);
            var clearPosition = (position + 1) % 14;
            var clear = SipLogFields.Find(transcript, 6 + clearPosition);
            Assert.AreEqual(6 + clearPosition, clear.Offset);
            Assert.AreEqual(1, clear.Length);
            StringAssert.Contains(clear.Description, "State: Not set (space).");
            Assert.AreEqual(field.FlagGroup, clear.FlagGroup);
            Assert.AreNotEqual(field, clear, "Moving between flags must update the tooltip and outline.");
        }
    }

    [TestMethod(DisplayName = "Blank and malformed patron-status flags have explicit states")]
    public void BlankAndUnexpectedFlagsAreNotConfusedWithSetConditions()
    {
        const string suffix = "00020261021    153000AOlibrary|";
        var blank = SipLogFields.Find("<-- 24" + new string(' ', 14) + suffix, 19);
        StringAssert.Contains(blank.Description, "State: Not set (space).");
        StringAssert.Contains(blank.Description, "Set flags: 0 of 14.");
        Assert.AreEqual(1, blank.Length);
        foreach (var unexpected in new[] { 'N', 'X', 'y' })
        {
            var invalid = SipLogFields.Find("<-- 24" + unexpected + new string(' ', 13) + suffix, 6);
            StringAssert.Contains(invalid.Description, $"Unrecognized flag value '{unexpected}'");
            StringAssert.Contains(invalid.Description, "Set flags: 0 of 14.");
            Assert.IsFalse(invalid.FlagGroup.IsSet(0));
            Assert.IsFalse(invalid.FlagGroup.IsValid(0));
        }
        Assert.IsNull(SipLogFields.Find("<-- 24Y    ", 6), "A partial status group must not guess missing flag positions.");
    }

    [TestMethod(DisplayName = "Summary flags explain detailed-information requests individually")]
    [DataRow(0, "Hold items")]
    [DataRow(1, "Overdue items")]
    [DataRow(2, "Checked-out items")]
    [DataRow(3, "Fine items")]
    [DataRow(4, "Recall items")]
    [DataRow(5, "Unavailable holds")]
    public void SummaryFlagsHaveIndividualHelp(int position, string category)
    {
        var flags = new string(' ', 10).ToCharArray();
        flags[position] = 'Y';
        var transcript = "--> 6300020261021    153000" + new string(flags) + "AOlibrary|";
        var field = SipLogFields.Find(transcript, 27 + position);
        Assert.AreEqual(27 + position, field.Offset);
        Assert.AreEqual(1, field.Length);
        Assert.AreEqual(category, field.Description.Split('\n')[1]);
        StringAssert.Contains(field.Description, "State: SET (Y).");
        StringAssert.Contains(field.Description, "Set flags: 1 of 6.");
        Assert.IsTrue(field.FlagGroup.IsSet(position));
    }

    [TestMethod(DisplayName = "Reserved summary flags are distinguished from real categories")]
    public void ReservedSummaryPositionsRemainExplicit()
    {
        for (var position = 6; position < 10; position++)
        {
            var flags = new string(' ', 10).ToCharArray();
            flags[position] = 'Y';
            var field = SipLogFields.Find("--> 6300020261021    153000" + new string(flags) + "AOlibrary|", 27 + position);
            StringAssert.Contains(field.Description, "Reserved position");
            StringAssert.Contains(field.Description, "Unexpected value 'Y' in a reserved position");
            StringAssert.Contains(field.Description, "Set flags: 0 of 6.");
            Assert.IsFalse(field.FlagGroup.IsSet(position));
            Assert.IsFalse(field.FlagGroup.IsValid(position));
        }
    }

    [TestMethod(DisplayName = "Supported-message flags use N for clear and retain identifier help")]
    public void SupportedMessageFlagsUseTheirOwnClearValue()
    {
        const string prefix = "<-- 98YYYYYY03000320261021    1530002.00AOlibrary|BX";
        const string flags = "YNNNNNYNNNNNNNNY";
        string[] names = ["Patron status request", "Checkout", "Checkin", "Block patron", "SC/ACS status", "SC/ACS resend", "Login", "Patron information", "End patron session", "Fee paid", "Item information", "Item status update", "Patron enable", "Hold", "Renew", "Renew all"];
        var transcript = prefix + flags + "|AFstatus|";
        for (var position = 0; position < flags.Length; position++)
        {
            var field = SipLogFields.Find(transcript, prefix.Length + position);
            Assert.AreEqual(prefix.Length + position, field.Offset);
            Assert.AreEqual(1, field.Length);
            Assert.AreEqual(names[position], field.Description.Split('\n')[1]);
            StringAssert.Contains(field.Description, flags[position] == 'Y' ? "State: SET (Y)." : "State: Not set (N).");
            StringAssert.Contains(field.Description, "Set flags: 3 of 16.");
            Assert.AreEqual('N', field.FlagGroup.ClearValue);
        }
        Assert.AreEqual("BX", SipLogFields.Find(transcript, prefix.Length - 2)?.Identifier);
        Assert.IsNull(SipLogFields.Find(prefix + "NN|", prefix.Length), "Incomplete supported-message lists must not guess missing flags.");
        var extension = SipLogFields.Find(prefix + flags + "Y|", prefix.Length + 16);
        StringAssert.Contains(extension.Description, "Unrecognized position (extension)");
        Assert.IsFalse(extension.FlagGroup.IsSet(16));
    }
}
