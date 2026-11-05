using Clc.Sip.Helpers;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class MessageExpansionTests
{
    [TestMethod(DisplayName = "Expanded fields retain wire order, repeated values, empty fields and trailers")]
    public void FieldsRetainValuesAndWireOrder()
    {
        const string message = "121YNN20261021    153000AOlibrary|AApatron|AJMoby Dick 🚢|CO|BMone|BMtwo|ZZcustom|AY7AZCDEF";
        var details = SipLogFields.DescribeMessage(message);
        Assert.AreEqual("Checkout response", details.Name);
        CollectionAssert.AreEqual(new[] { "12", "1", "Y", "N", "N", "20261021    153000", "library", "patron", "Moby Dick 🚢", "", "one", "two", "custom", "7", "CDEF" },
            details.Fields.Select(field => field.Value).ToArray());
        Assert.AreEqual("Message ID", details.Fields[0].Name);
        StringAssert.Contains(details.Fields[1].Meaning, "successfully");
        Assert.AreEqual("AO · Institution ID", details.Fields[6].Name);
        Assert.AreEqual("CO · Login password", details.Fields[9].Name);
        Assert.AreEqual("(empty)", details.Fields[9].DisplayValue);
        Assert.AreEqual("BM · Renewed items", details.Fields[10].Name);
        Assert.AreEqual(details.Fields[10].Name, details.Fields[11].Name);
        StringAssert.Contains(details.Fields[12].Description, "server-specific extension");
        Assert.AreEqual("AY7", message.Substring(details.Fields[13].Offset, details.Fields[13].Length));
        Assert.AreEqual("AZCDEF", message.Substring(details.Fields[14].Offset, details.Fields[14].Length));
        Assert.IsTrue(details.Fields.Zip(details.Fields.Skip(1)).All(pair => pair.First.Offset + pair.First.Length <= pair.Second.Offset));
    }

    [TestMethod(DisplayName = "Patron status expands into a parent and fourteen named flag rows")]
    [DataRow("24")]
    [DataRow("26")]
    [DataRow("64")]
    public void PatronStatusHasIndividualFlagRows(string code)
    {
        var flags = new string(' ', 14).ToCharArray();
        flags[0] = flags[4] = 'Y';
        flags[13] = 'N'; // N is invalid for patron status, rather than a clear flag.
        var message = code + new string(flags) + "00120261021    153000" + (code == "64" ? "000100020003000400050006" : "") + "AApatron|";
        var details = SipLogFields.DescribeMessage(message);
        var status = details.Fields[1];
        Assert.AreEqual("Patron status", status.Name);
        Assert.AreEqual(14, status.Children.Count);
        Assert.AreEqual(new string(flags), status.Value);
        StringAssert.Contains(status.Meaning, "2 of 14 flags set");
        Assert.AreEqual(SipLogValueState.Invalid, status.State);
        Assert.AreEqual("Borrowing privileges denied", status.Children[0].Description);
        Assert.AreEqual("SET (Y)", status.Children[0].Meaning);
        Assert.AreEqual("Not set (space)", status.Children[1].Meaning);
        Assert.AreEqual("Card reported lost", status.Children[4].Description);
        Assert.AreEqual(SipLogValueState.Set, status.Children[4].State);
        Assert.AreEqual("Too many billed items", status.Children[13].Description);
        StringAssert.Contains(status.Children[13].Meaning, "INVALID ('N')");
        for (var index = 0; index < 14; index++)
        {
            var child = status.Children[index];
            Assert.AreEqual(2 + index, child.Offset);
            Assert.AreEqual(message[child.Offset].ToString(), child.Value);
            StringAssert.Contains(child.Name, $"position {index}");
        }
        Assert.AreEqual("English.", details.Fields[2].Meaning);
        Assert.AreEqual("AA · Patron identifier", details.Fields[^1].Name);
    }

    [TestMethod(DisplayName = "Summary rows identify requested categories and reserved positions")]
    public void SummaryIncludesReservedPositions()
    {
        var details = SipLogFields.DescribeMessage("6300020261021    153000Y     Y   AOlibrary|");
        var summary = details.Fields.Single(field => field.Name == "Summary");
        Assert.AreEqual(10, summary.Children.Count);
        Assert.AreEqual("Hold items", summary.Children[0].Description);
        Assert.AreEqual("SET (Y)", summary.Children[0].Meaning);
        Assert.AreEqual("Reserved position", summary.Children[6].Description);
        Assert.AreEqual(SipLogValueState.Invalid, summary.Children[6].State);
        Assert.AreEqual("Reserved; blank as expected", summary.Children[9].Meaning);
        StringAssert.Contains(summary.Meaning, "1 of 6 flags set");
    }

    [TestMethod(DisplayName = "Supported messages expand Y/N flags and preserve unknown extensions")]
    public void SupportedMessagesUseNForClearAndRetainExtensions()
    {
        const string prefix = "98YYYYYY03000320261021    1530002.00BX";
        var details = SipLogFields.DescribeMessage(prefix + "YNNNNNYNNNNNNNNYX|AFstatus|");
        var supported = details.Fields.Single(field => field.Name == "BX · Supported messages");
        Assert.AreEqual(17, supported.Children.Count);
        Assert.AreEqual("Patron status request", supported.Children[0].Description);
        Assert.AreEqual("Not set (N)", supported.Children[1].Meaning);
        Assert.AreEqual("Login", supported.Children[6].Description);
        Assert.AreEqual("Renew all", supported.Children[15].Description);
        Assert.AreEqual("Unrecognized position (extension)", supported.Children[16].Description);
        StringAssert.Contains(supported.Children[16].Meaning, "'X'");
        StringAssert.Contains(supported.Meaning, "3 of 16 flags set");
        var partial = SipLogFields.DescribeMessage(prefix + "YN|").Fields.Last();
        Assert.AreEqual(2, partial.Children.Count);
        Assert.AreEqual(SipLogValueState.Invalid, partial.State);
        StringAssert.Contains(partial.Meaning, "Incomplete");
    }

    [TestMethod(DisplayName = "Unknown messages and malformed fixed fields keep uninterpreted data")]
    public void MalformedMessagesDoNotInventFieldsOrDropValues()
    {
        var unknown = SipLogFields.DescribeMessage("88AOambiguous|ZZextension|");
        CollectionAssert.AreEqual(new[] { "88", "AOambiguous", "extension" }, unknown.Fields.Select(field => field.Value).ToArray());
        Assert.AreEqual("Unrecognized header", unknown.Fields[1].Name);
        var truncated = SipLogFields.DescribeMessage("17short|AAvalue|");
        Assert.AreEqual(2, truncated.Fields.Count);
        Assert.AreEqual("short|AAvalue|", truncated.Fields[1].Value);
        Assert.AreEqual("Incomplete fixed header", truncated.Fields[1].Name);
        Assert.AreEqual(SipLogValueState.Invalid, truncated.Fields[1].State);
        var malformed = SipLogFields.DescribeMessage("941BHU|AZ12");
        Assert.AreEqual("U", malformed.Fields[2].Value);
        StringAssert.Contains(malformed.Fields[2].Meaning, "expected 3 characters");
        Assert.AreEqual("12", malformed.Fields[3].Value);
        Assert.AreEqual(SipLogValueState.Invalid, malformed.Fields[3].State);
        Assert.AreEqual("not SIP", SipLogFields.DescribeMessage("not SIP").Fields.Single().Value);
        Assert.AreEqual("", SipLogFields.DescribeMessage(null).Fields.Single().Value);
    }
}
