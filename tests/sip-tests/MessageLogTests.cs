using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class MessageLogTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

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

    [TestMethod(DisplayName = "Hover help keeps log formatting and selection intact")]
    public void MainFormWiresHoverHelpWithoutChangingTheTranscript()
    {
        using var form = new MainForm();
        ShowOffscreen(form);
        form.LogOutput(new SipTransaction
        {
            Message = "9300CNoperator|COpassword|VPprofile|AY0AZABCD\r",
            Response = "941AY0AZABCD\r"
        });
        var log = form.GetRichTextBox("logRichTextBox");
        var components = (IContainer)typeof(MainForm).GetField("components", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        var help = components.Components.OfType<MessageLogToolTips>().Single();
        log.Select(8, 8);
        var originalRtf = log.Rtf;
        var originalText = log.Text;
        foreach (var token in new[] { "CN", "CO", "VP", "AY", "AZ" })
        {
            var offset = log.Text.IndexOf(token, StringComparison.Ordinal);
            for (var index = offset; index < offset + 2; index++)
            {
                var point = log.GetPositionFromCharIndex(index);
                point.Offset(1, 2);
                var field = help.GetFieldAt(point);
                Assert.IsNotNull(field, token);
                Assert.AreEqual(token, field.Identifier);
            }
        }
        Assert.IsNull(help.GetFieldAt(new Point(log.ClientSize.Width - 5, log.ClientSize.Height - 5)), "Empty log space must not use the nearest character's help.");
        Assert.AreEqual(originalRtf, log.Rtf);
        Assert.AreEqual(originalText, log.Text);
        Assert.AreEqual(8, log.SelectionStart);
        Assert.AreEqual(8, log.SelectionLength);

        form.GetButton("clearLogButton").PerformClick();
        Assert.IsNull(help.GetFieldAt(new Point(3, 3)));
        form.LogOutput(new SipTransaction { Message = "9300COreplacement|\r", Response = "941\r" });
        var replacementPoint = log.GetPositionFromCharIndex(log.Text.IndexOf("CO", StringComparison.Ordinal));
        replacementPoint.Offset(1, 2);
        Assert.AreEqual("CO", help.GetFieldAt(replacementPoint)?.Identifier, "Clearing and appending must invalidate cached message text.");
    }

    [TestMethod(DisplayName = "Wrapped and scrolled field identifiers retain help")]
    public void HitTestingFollowsWrappedAndScrolledText()
    {
        using var form = new Form { ClientSize = new Size(260, 180) };
        using var components = new Container();
        using var log = new RichTextBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 10), WordWrap = true };
        form.Controls.Add(log);
        using var help = new MessageLogToolTips(log, components);
        ShowOffscreen(form);
        var splitIdentifier = -1;
        // Find a natural wrap between the two letters instead of assuming a DPI.
        for (var padding = 1; padding < 80; padding++)
        {
            log.Text = "--> 9300CN" + new string('x', padding) + "|AOlibrary|";
            var offset = log.Text.IndexOf("AO", StringComparison.Ordinal);
            if (log.GetPositionFromCharIndex(offset).Y != log.GetPositionFromCharIndex(offset + 1).Y)
            {
                splitIdentifier = offset;
                break;
            }
        }
        Assert.IsTrue(splitIdentifier >= 0, "Test setup did not produce a wrapped identifier.");
        foreach (var index in new[] { splitIdentifier, splitIdentifier + 1 })
        {
            var point = log.GetPositionFromCharIndex(index);
            point.Offset(1, 2);
            Assert.AreEqual("AO", help.GetFieldAt(point)?.Identifier);
        }

        log.Text = string.Concat(Enumerable.Repeat("--> 9300CNuser|COpassword|\n", 40)) + "<-- 941AY0AZABCD";
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
        var checksumPoint = log.GetPositionFromCharIndex(log.Text.LastIndexOf("AZ", StringComparison.Ordinal));
        checksumPoint.Offset(1, 2);
        Assert.AreEqual("AZ", help.GetFieldAt(checksumPoint)?.Identifier);
    }
}
