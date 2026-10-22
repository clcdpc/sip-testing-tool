using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class FixedFieldLogTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

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

    [TestMethod(DisplayName = "Hover and click outlines paint above native text without changing RTF")]
    [DataRow(false)]
    [DataRow(true)]
    public void HoverAndPinnedOutlinesPreserveTheRawLog(bool darkMode)
    {
        var previousTheme = AppTheme.IsDarkMode;
        try
        {
            AppTheme.SetDarkMode(darkMode);
            using var form = new Form { ClientSize = new Size(740, 180) };
            using var components = new Container();
            using var log = new TestLog
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11),
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.LogBackground,
                ForeColor = AppTheme.Text
            };
            form.Controls.Add(log);
            using var help = new MessageLogToolTips(log, components);
            ShowOffscreen(form);
            log.Text = "--> 11YN20261021    153000                  AOlibrary|AApatron|\n<-- 121YNN20261021    153000AOlibrary|";
            log.Select(0, 0);
            var originalRtf = log.Rtf;
            using var before = Render(log);
            var point = PointAt(log, 8);
            log.MoveTo(point);
            Assert.AreEqual("Transaction date/time", help.OutlinedField?.Identifier);
            Assert.AreEqual(18, help.OutlinedField.Length);
            using (var hovered = Render(log))
            {
                AssertOutlinePixels(before, hovered, help.GetOutlineBounds(help.OutlinedField));
            }
            log.ClickAt(point);
            log.MoveAway();
            Assert.AreEqual("Transaction date/time", help.OutlinedField?.Identifier, "Click must keep the outline after the pointer leaves.");
            using (var pinned = Render(log))
            {
                AssertOutlinePixels(before, pinned, help.GetOutlineBounds(help.OutlinedField));
                pinned.Save(Path.Combine(AppContext.BaseDirectory, darkMode ? "fixed-field-outline-dark.png" : "fixed-field-outline-light.png"));
            }
            Assert.AreEqual(originalRtf, log.Rtf);
            Assert.AreEqual(0, log.SelectionStart);
            Assert.AreEqual(0, log.SelectionLength);

            log.MoveTo(PointAt(log, 26));
            Assert.AreEqual("No-block due date/time", help.OutlinedField?.Identifier, "Blank fixed values must have an outline.");
            log.MoveAway();
            Assert.AreEqual("Transaction date/time", help.OutlinedField?.Identifier, "Leaving a hover should restore the clicked field.");
            log.Escape();
            Assert.IsNull(help.OutlinedField);
            using var cleared = Render(log);
            AssertSamePixels(before, cleared);

            log.ClickAt(point);
            log.ClickAt(new Point(log.ClientSize.Width - 5, log.ClientSize.Height - 5));
            Assert.IsNull(help.OutlinedField, "Clicking empty space must clear the pinned field.");
            log.ClickAt(point);
            log.BeginSelection(point);
            log.Select(4, 24);
            log.EndSelection(PointAt(log, 28));
            Assert.IsNull(help.OutlinedField, "Selecting text should dismiss the outline.");
            Assert.AreEqual(log.Text.Substring(4, 24), log.SelectedText);

            log.Select(0, 0);
            log.ClickAt(point);
            var pinnedField = help.OutlinedField;
            AppTheme.SetDarkMode(!darkMode);
            log.BackColor = AppTheme.LogBackground;
            log.RecolorTranscript();
            Assert.AreEqual(pinnedField, help.OutlinedField, "Recoloring the transcript must not confuse formatting selections with user clicks.");
        }
        finally { AppTheme.SetDarkMode(previousTheme); }
    }

    [TestMethod(DisplayName = "Fixed outlines follow wrapping, scrolling, resizing and handle recreation")]
    public void FixedOutlineGeometryFollowsTheNativeLayout()
    {
        using var form = new Form { ClientSize = new Size(190, 200) };
        using var components = new Container();
        using var log = new TestLog { Dock = DockStyle.Fill, Font = new Font("Consolas", 11), BorderStyle = BorderStyle.None, WordWrap = true };
        form.Controls.Add(log);
        using var help = new MessageLogToolTips(log, components);
        ShowOffscreen(form);
        const string transaction = "--> 11YN20261021    153000                  AOlibrary|";
        log.Text = transaction;
        log.MoveTo(PointAt(log, 8));
        var field = help.OutlinedField;
        Assert.IsNotNull(field);
        var rows = help.GetOutlineBounds(field);
        Assert.IsTrue(rows.Count > 1, "The timestamp should wrap in this narrow log.");
        for (var index = field.Offset; index < field.Offset + field.Length; index++)
        {
            var point = PointAt(log, index);
            Assert.AreEqual(field, help.GetFieldAt(point), "Every timestamp character, including spaces, should resolve to the same field.");
            Assert.IsTrue(rows.Any(row => row.Contains(point)));
        }
        log.ClickAt(PointAt(log, 8));
        form.ClientSize = new Size(740, 200);
        Assert.AreEqual(field, help.OutlinedField);
        Assert.AreEqual(1, help.GetOutlineBounds(field).Count);
        log.RecreateNativeHandle();
        log.MoveTo(PointAt(log, 8));
        Assert.AreEqual(field, help.OutlinedField);

        log.Text = string.Join('\n', Enumerable.Repeat(transaction, 40)) + "\n";
        Assert.IsNull(help.OutlinedField, "Changing the transcript must clear stale field ranges.");
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
        var lastOffset = log.Text.LastIndexOf("20261021", StringComparison.Ordinal);
        var lastPoint = PointAt(log, lastOffset);
        Assert.IsTrue(log.ClientRectangle.Contains(lastPoint), "The test timestamp must be inside the visible viewport.");
        log.MoveTo(lastPoint);
        Assert.AreEqual(lastOffset, help.OutlinedField?.Offset,
            $"Point {lastPoint}, client {log.ClientRectangle}, nearest index {log.GetCharIndexFromPosition(lastPoint)}, hit {help.GetFieldAt(lastPoint)}, selected length {log.SelectionLength}.");
        Assert.AreEqual(18, help.OutlinedField.Length);
        Assert.IsTrue(help.GetOutlineBounds(help.OutlinedField).All(row => row.IntersectsWith(log.ClientRectangle)));
        log.Clear();
        Assert.IsNull(help.OutlinedField);
        Assert.IsNull(help.GetFieldAt(new Point(2, 2)));
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

    [TestMethod(DisplayName = "Flag inspection outlines one cell and marks set flags without changing raw text")]
    [DataRow(false)]
    [DataRow(true)]
    public void FlagOutlinesShowIndividualCellsAndSetMarkers(bool darkMode)
    {
        var previousTheme = AppTheme.IsDarkMode;
        try
        {
            AppTheme.SetDarkMode(darkMode);
            using var form = new Form { ClientSize = new Size(740, 180) };
            using var components = new Container();
            using var log = new TestLog
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11),
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.LogBackground,
                ForeColor = AppTheme.Text
            };
            form.Controls.Add(log);
            using var help = new MessageLogToolTips(log, components);
            ShowOffscreen(form);
            log.Text = "<-- 24Y   Y        Y00020261021    153000AOlibrary|AApatron|";
            log.Select(0, 0);
            var originalText = log.Text;
            var originalRtf = log.Rtf;
            using var before = Render(log);
            log.MoveTo(PointAt(log, 10));
            var field = help.OutlinedField;
            Assert.AreEqual(10, field?.Offset);
            Assert.AreEqual(1, field.Length);
            StringAssert.Contains(field.Description, "Card reported lost\nState: SET (Y).");
            StringAssert.Contains(field.Description, "Set flags: 3 of 14.");
            using (var hovered = Render(log))
            {
                var groupBounds = help.GetOutlineBounds(field with { Offset = field.FlagGroup.Offset, Length = field.FlagGroup.Value.Length });
                AssertOutlinePixels(before, hovered, groupBounds);
                var active = help.GetOutlineBounds(field with { Offset = 6 })[0];
                var inactive = help.GetOutlineBounds(field with { Offset = 8 })[0];
                Assert.AreNotEqual(before.GetPixel(active.X + 2, active.Bottom - 2), hovered.GetPixel(active.X + 2, active.Bottom - 2), "Set flags need a visible underline marker.");
                Assert.AreEqual(before.GetPixel(inactive.X + 2, inactive.Bottom - 2), hovered.GetPixel(inactive.X + 2, inactive.Bottom - 2), "Clear flags should not have set markers.");
                hovered.Save(Path.Combine(AppContext.BaseDirectory, darkMode ? "patron-flags-dark.png" : "patron-flags-light.png"));
            }
            log.MoveTo(PointAt(log, 7));
            Assert.AreEqual(7, help.OutlinedField?.Offset);
            StringAssert.Contains(help.OutlinedField.Description, "Renewal privileges denied\nState: Not set (space).");
            log.ClickAt(PointAt(log, 7));
            log.MoveAway();
            Assert.AreEqual(7, help.OutlinedField?.Offset);
            Assert.AreEqual(1, help.OutlinedField.Length);
            Assert.AreEqual(originalText, log.Text);
            Assert.AreEqual(originalRtf, log.Rtf);
            Assert.AreEqual(0, log.SelectionStart);
            Assert.AreEqual(0, log.SelectionLength);
            log.Escape();
            Assert.IsNull(help.OutlinedField);
            using var cleared = Render(log);
            AssertSamePixels(before, cleared);
        }
        finally { AppTheme.SetDarkMode(previousTheme); }
    }

    [TestMethod(DisplayName = "Individual blank and set flags remain hoverable across wrapped rows")]
    public void FlagCellsFollowWrappedTextAndResize()
    {
        using var form = new Form { ClientSize = new Size(110, 180) };
        using var components = new Container();
        using var log = new TestLog { Dock = DockStyle.Fill, Font = new Font("Consolas", 11), BorderStyle = BorderStyle.None, WordWrap = true };
        form.Controls.Add(log);
        using var help = new MessageLogToolTips(log, components);
        ShowOffscreen(form);
        log.Text = "<-- 24Y   Y        Y00020261021    153000AOlibrary|";
        var firstRow = log.GetPositionFromCharIndex(6).Y;
        Assert.IsTrue(log.GetPositionFromCharIndex(19).Y > firstRow, "The test status field must span wrapped rows.");
        for (var position = 0; position < 14; position++)
        {
            var point = PointAt(log, 6 + position);
            if (!log.ClientRectangle.Contains(point))
            {
                continue; // RichEdit can place trailing wrap spaces past the viewport.
            }
            log.MoveTo(point);
            Assert.AreEqual(6 + position, help.OutlinedField?.Offset,
                $"Flag {position}, point {point}, client {log.ClientRectangle}, nearest index {log.GetCharIndexFromPosition(point)}.");
            Assert.AreEqual(1, help.OutlinedField.Length);
            Assert.IsTrue(help.GetOutlineBounds(help.OutlinedField).Single().Contains(point));
        }
        log.ClickAt(PointAt(log, 19));
        log.MoveAway();
        form.ClientSize = new Size(740, 180);
        Assert.AreEqual(19, help.OutlinedField?.Offset);
        Assert.IsTrue(help.GetOutlineBounds(help.OutlinedField).Single().Contains(PointAt(log, 19)));
        log.Clear();
        Assert.IsNull(help.OutlinedField);
    }

    private static Point PointAt(RichTextBox log, int index)
    {
        var point = log.GetPositionFromCharIndex(index);
        point.Offset(1, 2);
        return point;
    }

    private static Bitmap Render(RichTextBox log)
    {
        var bitmap = new Bitmap(log.ClientSize.Width, log.ClientSize.Height);
        log.DrawToBitmap(bitmap, log.ClientRectangle);
        return bitmap;
    }

    private static void AssertOutlinePixels(Bitmap before, Bitmap after, IReadOnlyList<Rectangle> rows)
    {
        var changed = 0;
        for (var y = 0; y < before.Height; y++)
        {
            for (var x = 0; x < before.Width; x++)
            {
                if (before.GetPixel(x, y) == after.GetPixel(x, y))
                {
                    continue;
                }
                changed++;
                Assert.IsTrue(rows.Any(row => Rectangle.Inflate(row, 1, 0).Contains(x, y)), $"Outline changed a pixel outside the fixed field at {x}, {y}.");
            }
        }
        Assert.IsTrue(changed > 10, "The fixed-field outline was not painted in the native preview.");
    }

    private static void AssertSamePixels(Bitmap expected, Bitmap actual)
    {
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.AreEqual(expected.GetPixel(x, y), actual.GetPixel(x, y), $"A stale outline remains at {x}, {y}.");
            }
        }
    }

    [TestMethod(DisplayName = "Rapid hover changes repaint only outline regions without erasing the transcript")]
    [DataRow(false)]
    [DataRow(true)]
    public void RapidHoverDoesNotFlashTheTranscript(bool darkMode)
    {
        var previousTheme = AppTheme.IsDarkMode;
        try
        {
            AppTheme.SetDarkMode(darkMode);
            using var form = new Form { ClientSize = new Size(740, 240) };
            using var components = new Container();
            using var log = new TestLog
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = false,
                BackColor = AppTheme.LogBackground,
                ForeColor = AppTheme.Text
            };
            form.Controls.Add(log);
            using var help = new MessageLogToolTips(log, components);
            ShowOffscreen(form);
            log.Text = "--> 11YN20261021    153000                  AOlibrary|AApatron|ABitem|AY0AZABCD\n" +
                "<-- 24Y   Y         00120261021    153000AOlibrary|AApatron|AEReader|AY0AZABCD";
            log.Select(4, 3);
            Application.DoEvents();
            var text = log.Text;
            var rtf = log.Rtf;
            var identifiers = new[] { text.IndexOf("AO", StringComparison.Ordinal), text.IndexOf("AA", StringComparison.Ordinal), text.IndexOf("AB", StringComparison.Ordinal) };
            log.TrackPainting();
            foreach (var index in Enumerable.Range(0, 10).SelectMany(_ => identifiers))
            {
                log.MoveTo(PointAt(log, index));
                log.Update();
            }
            log.MoveAway();
            log.Update();
            Assert.AreEqual(0, log.PaintBounds.Count, $"Identifier-only hover should not repaint the transcript; observed {log.BackgroundErases} background erases.");

            var response = text.IndexOf("<--", StringComparison.Ordinal);
            var fixedValues = new[] { 8, 26, response + 6, response + 10, response + 23, text.LastIndexOf("AZ", StringComparison.Ordinal) };
            foreach (var index in Enumerable.Range(0, 10).SelectMany(_ => fixedValues))
            {
                var previous = OutlineRows(help.OutlinedField);
                var paints = log.PaintBounds.Count;
                log.MoveTo(PointAt(log, index));
                Assert.IsNotNull(help.OutlinedField);
                var current = OutlineRows(help.OutlinedField);
                log.Update();
                foreach (var row in previous.Concat(current))
                {
                    Assert.IsTrue(log.PaintBounds.Skip(paints).Any(bounds => bounds.Contains(row)),
                        "Repainting must cover both the previous and current outline, including the whole flag group, to avoid stale borders.");
                }
            }
            Assert.IsTrue(log.PaintBounds.Count > 0, "The outlines must still update while hovering fixed fields.");
            Assert.IsTrue(log.PaintBounds.All(bounds => bounds != log.ClientRectangle), "Hover changes must not repaint the entire log.");
            Assert.AreEqual(0, log.BackgroundErases, "Hover painting must not blank the native RichEdit background before redrawing text.");
            log.TrackPainting();
            log.MoveTo(PointAt(log, fixedValues[^1] + 1));
            log.Update();
            Assert.AreEqual(0, log.PaintBounds.Count, "Moving within the same outlined field must not repaint it again.");
            log.MoveAway();
            log.Update();
            Assert.IsNull(help.OutlinedField);
            Assert.AreEqual(text, log.Text);
            Assert.AreEqual(rtf, log.Rtf);
            Assert.AreEqual(4, log.SelectionStart);
            Assert.AreEqual(3, log.SelectionLength);

            log.Select(0, 0);
            log.ClickAt(PointAt(log, 8));
            log.MoveAway();
            log.Update();
            var pinned = help.OutlinedField;
            Assert.IsNotNull(pinned);
            log.TrackPainting();
            foreach (var index in identifiers)
            {
                log.MoveTo(PointAt(log, index));
                log.Update();
            }
            Assert.AreEqual(pinned, help.OutlinedField);
            Assert.AreEqual(0, log.PaintBounds.Count, "Unchanged pinned outlines must not trigger repaints while crossing field identifiers.");

            log.Escape();
            log.Text = string.Join('\n', Enumerable.Repeat("<-- 24Y   Y         00120261021    153000AOlibrary|AApatron|", 40));
            log.Select(0, 0);
            log.ScrollToCaret();
            Application.DoEvents();
            var secondGroup = log.Text.IndexOf('\n') + 1 + 6;
            log.MoveTo(PointAt(log, secondGroup));
            log.Update();
            var scrolledField = help.OutlinedField;
            var previousRows = OutlineRows(scrolledField);
            Assert.IsNotNull(scrolledField?.FlagGroup);
            log.TrackPainting();
            log.ScrollOneLine();
            log.Update();
            var relocatedRows = OutlineRows(scrolledField);
            Assert.AreNotEqual(previousRows[0].Y, relocatedRows[0].Y);
            Assert.IsNull(help.OutlinedField, "Scrolling must dismiss hover.");
            Assert.IsTrue(relocatedRows.All(row => log.PaintBounds.Any(bounds => bounds.Contains(row))),
                "Scrolling must clear flag borders carried with the text to their new positions.");

            IReadOnlyList<Rectangle> OutlineRows(SipLogField field) => help.GetOutlineBounds(field?.FlagGroup == null ? field :
                field with { Offset = field.FlagGroup.Offset, Length = field.FlagGroup.Value.Length });
        }
        finally { AppTheme.SetDarkMode(previousTheme); }
    }

    private sealed class TestLog : RichTextBox
    {
        private bool trackPainting;
        public List<Rectangle> PaintBounds { get; } = new();
        public int BackgroundErases { get; private set; }
        public void TrackPainting()
        {
            PaintBounds.Clear();
            BackgroundErases = 0;
            trackPainting = true;
        }

        protected override void WndProc(ref Message message)
        {
            if (trackPainting && message.Msg == 0x000F && GetUpdateRect(Handle, out var bounds, false))
            {
                PaintBounds.Add(Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            }
            if (trackPainting && message.Msg == 0x0014)
            {
                BackgroundErases++;
            }
            base.WndProc(ref message);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetUpdateRect(IntPtr window, out NativeRect bounds, [MarshalAs(UnmanagedType.Bool)] bool erase);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        public void ScrollOneLine() => SendMessage(Handle, 0x0115, new IntPtr(1), IntPtr.Zero); // WM_VSCROLL, SB_LINEDOWN
        public void MoveTo(Point point) => OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
        public void MoveAway() => OnMouseLeave(EventArgs.Empty);
        public void ClickAt(Point point)
        {
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
        }
        public void Escape() => OnKeyDown(new KeyEventArgs(Keys.Escape));
        public void BeginSelection(Point point) => OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
        public void EndSelection(Point point) => OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
        public void RecreateNativeHandle() => RecreateHandle();
    }
}
