using Clc.Sip.Helpers;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class MessageLogTests
{
    [TestMethod(DisplayName = "First field after request and response headers")]
    [DataRow("9300CNoperator|COpassword|", "CN", "Login user ID")]
    [DataRow("1720261021    153000AOlibrary|ABitem|", "AO", "Institution ID")]
    [DataRow("11YN20261021    153000                  AOlibrary|AApatron|", "AO", "Institution ID")]
    [DataRow("09N20261021    15300020261021    150000APdesk|AOlibrary|", "AP", "Current location")]
    [DataRow("6300020261021    153000Y         AOlibrary|AApatron|", "AO", "Institution ID")]
    [DataRow("430120261021    15300020261021    150000001002AOlibrary|DEcomment|", "AO", "Institution ID")]
    [DataRow("121YNN20261021    153000AOlibrary|AApatron|", "AO", "Institution ID")]
    [DataRow("101YNN20261021    153000AOlibrary|ABitem|", "AO", "Institution ID")]
    [DataRow("1803010120261021    153000ABitem|AJtitle|", "AB", "Item identifier")]
    [DataRow("24              00020261021    153000AOlibrary|AApatron|", "AO", "Institution ID")]
    [DataRow("64              00020261021    153000000100020003000400050006AOlibrary|AApatron|", "AO", "Institution ID")]
    [DataRow("98YYYYYY03000320261021    1530002.00AOlibrary|AMLibrary|", "AO", "Institution ID")]
    public void FirstFieldsFollowTheMessageLayout(string message, string identifier, string name)
    {
        foreach (var prefix in new[] { "--> ", "<-- " })
        {
            var transcript = prefix + message;
            var offset = transcript.IndexOf(identifier, StringComparison.Ordinal);
            foreach (var index in new[] { offset, offset + 1 })
            {
                var field = SipLogFields.Find(transcript, index);
                Assert.IsNotNull(field, message);
                Assert.AreEqual(offset, field.Offset);
                Assert.AreEqual(identifier, field.Identifier);
                StringAssert.Contains(field.Description, name);
            }
        }
    }

    [TestMethod(DisplayName = "Log help recognizes repeated fields, empty values and extensions")]
    public void HelpExplainsFieldsInMultipleTransactions()
    {
        const string transcript = "--> 9300CNuser|CO|VPprofile|AY0AZABCD\n<-- 941AY0AZABCD\n\n" +
            "--> 430120261021    15300020261021    150000001002AOlibrary|DEcomment|AY1AZABCD\n" +
            "<-- 6600002000120261021    153000AOlibrary|BMone|BMtwo|BNthree|ZZextension|AY1AZABCD";
        foreach (var (token, name) in new[]
        {
            ("CO|", "Login password"), ("VPprofile", "Vendor profile (extension)"),
            ("DEcomment", "Notice comment (extension)"), ("BMone", "Renewed items"),
            ("BMtwo", "Renewed items"), ("BNthree", "Unrenewed items"), ("ZZextension", "Unrecognized field")
        })
        {
            var field = SipLogFields.Find(transcript, transcript.IndexOf(token, StringComparison.Ordinal));
            Assert.IsNotNull(field, token);
            StringAssert.Contains(field.Description, name);
        }
    }

    [TestMethod(DisplayName = "Sequence and checksum identifiers need no intervening delimiter")]
    [DataRow("--> 9901002.00AY0AZABCD")]
    [DataRow("<-- 941AY9AZABCD")]
    [DataRow("<-- 96AZABCD")]
    [DataRow("--> 9300CNuser|COpassword|AY0AZABCD")]
    public void TrailersHaveHelpWithAndWithoutVariableFields(string transcript)
    {
        foreach (var (identifier, name) in new[] { ("AY", "Sequence number"), ("AZ", "Checksum") })
        {
            var offset = transcript.IndexOf(identifier, StringComparison.Ordinal);
            if (offset < 0)
            {
                continue;
            }
            var field = SipLogFields.Find(transcript, offset + 1);
            Assert.IsNotNull(field, transcript);
            Assert.AreEqual(offset, field.Offset);
            StringAssert.Contains(field.Description, name);
            Assert.IsTrue(field.IsFixed);
            Assert.AreEqual(identifier == "AY" ? 3 : 6, field.Length);
            Assert.AreEqual(field, SipLogFields.Find(transcript, offset + 2), "A fixed trailer value should have the same help as its identifier.");
        }
    }

    [TestMethod(DisplayName = "Variable values and delimiters have no help")]
    public void VariableValuesDelimitersAndBlankLinesHaveNoHelp()
    {
        const string transcript = "--> 93AOCNAOvalue AA AB AY0AZFACE|CO|AY0AZABCD\n\n<-- 941";
        var ranges = new[]
        {
            (Offset: transcript.IndexOf("CN", StringComparison.Ordinal), Length: 2),
            (Offset: transcript.IndexOf("CO|", StringComparison.Ordinal), Length: 2),
            (Offset: transcript.LastIndexOf("AY", StringComparison.Ordinal), Length: 3),
            (Offset: transcript.LastIndexOf("AZ", StringComparison.Ordinal), Length: 6)
        };
        for (var index = 0; index < transcript.Length; index++)
        {
            var field = SipLogFields.Find(transcript, index);
            var responseStart = transcript.LastIndexOf("<-- ", StringComparison.Ordinal) + 4;
            var fixedValue = (index >= 4 && index < 8) || (index >= responseStart && index < responseStart + 3);
            Assert.AreEqual(fixedValue || ranges.Any(range => index >= range.Offset && index < range.Offset + range.Length), field != null, $"Unexpected help at index {index}.");
            if (fixedValue)
            {
                Assert.IsTrue(field.IsFixed);
            }
        }
        Assert.IsNull(SipLogFields.Find(transcript, -1));
        Assert.IsNull(SipLogFields.Find(transcript, transcript.Length));
        Assert.IsNull(SipLogFields.Find("", 0));
        Assert.IsNull(SipLogFields.Find(null, 0));
    }

    [TestMethod(DisplayName = "Unknown and incomplete packets do not guess inside fixed headers")]
    public void UnknownAndTruncatedMessagesRemainSafe()
    {
        const string transcript = "<-- 88headerAAvalue|ZZcustom|AApatron|\r\n--> 17AOshort|\r\n--> 9\r\nplain text |AAvalue|";
        Assert.IsNull(SipLogFields.Find(transcript, transcript.IndexOf("AAvalue", StringComparison.Ordinal)));
        var unknown = SipLogFields.Find(transcript, transcript.IndexOf("ZZ", StringComparison.Ordinal));
        Assert.IsNotNull(unknown);
        StringAssert.Contains(unknown.Description, "server-specific extension");
        var patron = SipLogFields.Find(transcript, transcript.IndexOf("AApatron", StringComparison.Ordinal));
        Assert.IsNotNull(patron);
        StringAssert.Contains(patron.Description, "Patron identifier");
        Assert.IsNull(SipLogFields.Find(transcript, transcript.IndexOf("AOshort", StringComparison.Ordinal)));
        Assert.IsNull(SipLogFields.Find(transcript, transcript.LastIndexOf("AAvalue", StringComparison.Ordinal)));
    }
}
