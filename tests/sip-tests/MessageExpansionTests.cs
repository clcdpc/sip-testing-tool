using System.Drawing;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class MessageExpansionTests
{
    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

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

    [TestMethod(DisplayName = "Message buttons open themed flag tables while preserving transcript and selection")]
    [DataRow(false)]
    [DataRow(true)]
    public void MessageButtonsExpandSwitchCollapseAndClear(bool darkMode)
    {
        var previousTheme = AppTheme.IsDarkMode;
        try
        {
            AppTheme.SetDarkMode(darkMode);
            using var form = new MainForm();
            ShowOffscreen(form);
            form.LogOutput(new SipTransaction
            {
                Message = "2300120261021    153000AOlibrary|AApatron|AY0AZABCD\r",
                Response = "24Y   Y         00120261021    153000AOlibrary|AApatron|AEAlex Reader|AY0AZABCD\r"
            });
            Application.DoEvents();
            var log = form.GetRichTextBox("logRichTextBox");
            var gutter = form.GetPanel("messageLogGutter");
            var split = form.GetSplitContainer("messageLogSplit");
            var grid = form.GetDataGridView("messageFieldsGrid");
            Assert.IsTrue(split.Panel2Collapsed);
            Assert.AreEqual(2, gutter.Controls.Count);
            foreach (Button button in gutter.Controls)
            {
                using var glyph = new Bitmap(button.Width, button.Height);
                button.DrawToBitmap(glyph, new Rectangle(Point.Empty, glyph.Size));
                Assert.AreEqual(AppTheme.Text.ToArgb(), glyph.GetPixel(button.Width / 2, button.Height / 2 - 2).ToArgb(), "The plus sign must fit inside the line-height button.");
            }
            log.Select(4, 3);
            var text = log.Text;
            var rtf = log.Rtf;
            var responseOffset = text.IndexOf("<--", StringComparison.Ordinal);
            form.GetButton($"expandMessage{responseOffset}Button").PerformClick();
            Application.DoEvents();
            Assert.IsFalse(split.Panel2Collapsed);
            StringAssert.Contains(form.GetControl("messageDetailsHeading").Text, "Received message 2 · Patron status response");
            Assert.AreEqual("Patron status", grid.Rows[1].Cells[0].Value);
            Assert.AreEqual("Borrowing privileges denied", grid.Rows[2].Cells[1].Value);
            Assert.AreEqual("SET (Y)", grid.Rows[2].Cells[2].Value);
            Assert.AreEqual("Not set (space)", grid.Rows[3].Cells[2].Value);
            Assert.IsTrue(grid.Rows[2].Cells[2].Style.Font.Bold);
            Assert.AreEqual("Card reported lost", grid.Rows[6].Cells[1].Value);
            Assert.AreEqual(text, log.Text);
            Assert.AreEqual(rtf, log.Rtf);
            Assert.AreEqual(4, log.SelectionStart);
            Assert.AreEqual(3, log.SelectionLength);
            Assert.AreEqual(text.Substring(4, 3), log.SelectedText);
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, darkMode ? "expanded-message-dark.png" : "expanded-message-light.png"));
            }
            grid.Rows[2].Cells[2].Selected = true;
            StringAssert.Contains(grid.GetClipboardContent().GetText(), "SET (Y)");
            grid.ClearSelection();

            AppTheme.SetDarkMode(!darkMode);
            Application.DoEvents();
            Assert.AreEqual(AppTheme.LogBackground, grid.DefaultCellStyle.BackColor);
            Assert.AreEqual(AppTheme.Text, grid.DefaultCellStyle.ForeColor);
            Assert.AreEqual("SET (Y)", grid.Rows[2].Cells[2].Value);
            Assert.IsFalse(split.Panel2Collapsed);
            form.GetButton("expandMessage0Button").PerformClick();
            Application.DoEvents();
            StringAssert.Contains(form.GetControl("messageDetailsHeading").Text, "Sent message 1 · Patron status request");
            Assert.AreEqual(0, grid.Rows.Cast<DataGridViewRow>().Count(row => row.Cells[0].Value.ToString().StartsWith("↳")));
            form.GetButton("expandMessage0Button").PerformClick();
            Application.DoEvents();
            Assert.IsTrue(split.Panel2Collapsed, "The same message's minus button must collapse its table.");
            form.GetButton($"expandMessage{responseOffset}Button").PerformClick();
            Application.DoEvents();
            var rowCount = grid.Rows.Count;
            form.LogOutput(new SipTransaction { Message = "9901002.00\r", Response = "941\r" });
            Application.DoEvents();
            Assert.IsFalse(split.Panel2Collapsed);
            Assert.AreEqual(rowCount, grid.Rows.Count, "New traffic must preserve the message being inspected.");
            form.GetButton("collapseMessageButton").PerformClick();
            Assert.IsTrue(split.Panel2Collapsed);
            form.GetButton("expandMessage0Button").PerformClick();
            Application.DoEvents();
            form.GetButton("clearLogButton").PerformClick();
            Application.DoEvents();
            Assert.IsTrue(split.Panel2Collapsed);
            Assert.AreEqual(0, grid.Rows.Count);
            Assert.AreEqual(0, gutter.Controls.Count);
        }
        finally { AppTheme.SetDarkMode(previousTheme); }
    }

    [TestMethod(DisplayName = "Expand buttons follow wrapped messages, scrolling, resizing and native handle recreation")]
    public void ButtonsFollowNativeTranscriptLayout()
    {
        using var form = new Form { ClientSize = new Size(340, 240) };
        var log = new RecreatedLog { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 10), WordWrap = true };
        using var view = new MessageLogView(log) { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        ShowOffscreen(form);
        log.Text = string.Join('\n', Enumerable.Range(0, 40).Select(index => $"--> 1720261021    153000ABitem-{index}|AJ{new string('x', 90)}|"));
        Application.DoEvents();
        AssertAligned();
        Assert.IsTrue(view.GetPanel("messageLogGutter").Controls.Count < 40, "Only visible message starts need controls.");
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
        Application.DoEvents();
        AssertAligned();
        var lastOffset = log.Text.LastIndexOf("--> ", StringComparison.Ordinal);
        Assert.IsTrue(view.GetButton($"expandMessage{lastOffset}Button").Visible);
        Assert.AreEqual(0, view.Controls.Find("expandMessage0Button", true).Length);
        form.ClientSize = new Size(600, 320);
        Application.DoEvents();
        AssertAligned();
        log.RecreateNativeHandle();
        Application.DoEvents();
        AssertAligned();

        void AssertAligned()
        {
            var gutter = view.GetPanel("messageLogGutter");
            Assert.IsTrue(gutter.Controls.Count > 0);
            foreach (Button button in gutter.Controls)
            {
                var point = log.GetPositionFromCharIndex((int)button.Tag);
                Assert.AreEqual(point.Y, button.Top);
                Assert.IsTrue(point.Y >= 0 && point.Y < log.ClientSize.Height);
                Assert.IsTrue(button.AccessibleName.StartsWith("Expand sent message"));
            }
        }
    }

    [TestMethod(DisplayName = "Large message tables use bounded sizing passes when switching, reopening, resizing and changing themes")]
    [DataRow(false)]
    [DataRow(true)]
    public void LargeMessagesAreSizedInBatches(bool darkMode)
    {
        // Same field layout and vendor extensions as the reported slow message,
        // with synthetic patron details.
        const string response = "64YYYY      Y   00120261022    083815000100000000000600000000" +
            "AO123|AA12345678901234|AEUSER, TEST ACCOUNT|BZ0060|CA0112|CB0117|BLY|CQY|BHUSD|BV20.89|CC10.00|" +
            "BD456 Example Avenue, EXAMPLE CITY, OH 12345|BEreader@example.test|BF5550101234|BC19890101    000000|" +
            "PA1|PEFLOCK|PS|U1|U2NONE or UNUSED|U3NONE or UNUSED|U4YES Computer Access|U5|PZ12345|" +
            "PX22220124    235959|PYN|FA0.00|PC1234567|PB12345678901234|" +
            "AFPatron has too many fines.|AGPatron has too many fines.|AY1AZ81AC";
        var previousTheme = AppTheme.IsDarkMode;
        try
        {
            AppTheme.SetDarkMode(darkMode);
            using var form = new MainForm();
            ShowOffscreen(form);
            form.LogOutput(new SipTransaction { Message = "6300020261022    083815          AO123|AA12345678901234|", Response = response });
            Application.DoEvents();
            var log = form.GetRichTextBox("logRichTextBox");
            var transcript = log.Text;
            var grid = form.GetDataGridView("messageFieldsGrid");
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.CellTemplate = new MeasuringCell();
            }
            var responseOffset = transcript.IndexOf("<--", StringComparison.Ordinal);
            AssertBoundedSizing(() => Expand(responseOffset), "Opening a large message");
            Assert.AreEqual(57, grid.Rows.Count);
            Assert.AreEqual(14, grid.Rows.Cast<DataGridViewRow>().Count(row => row.Cells[0].Value.ToString().StartsWith("↳")));
            AssertBoundedSizing(() => Expand(0), "Switching to a smaller message");
            AssertBoundedSizing(() => Expand(responseOffset), "Switching back to a large message");
            form.GetButton("collapseMessageButton").PerformClick();
            Application.DoEvents();
            AssertBoundedSizing(() => Expand(responseOffset), "Reopening a large message");
            AssertBoundedSizing(() => AppTheme.SetDarkMode(!darkMode), "Changing the theme");
            AssertBoundedSizing(() => form.Width -= 200, "Resizing a large message");
            Assert.AreEqual(57, grid.Rows.Count);
            Assert.AreEqual(transcript, log.Text);
            Assert.AreEqual("SET (Y)", grid.Rows[2].Cells[2].Value);
            Assert.IsTrue(grid.Rows[2].Cells[2].Style.Font.Bold);
            Assert.AreEqual("Not set (space)", grid.Rows[6].Cells[2].Value);
            Assert.AreEqual(AppTheme.Surface, grid.Rows[1].DefaultCellStyle.BackColor);
            Assert.IsTrue(grid.Rows.Cast<DataGridViewRow>().Any(row => row.Height > grid.RowTemplate.Height), "Descriptions should still wrap at narrow widths.");
            foreach (DataGridViewRow row in grid.Rows)
            {
                Assert.IsTrue(row.Height >= row.GetPreferredHeight(row.Index, DataGridViewAutoSizeRowMode.AllCellsExceptHeader, fixedWidth: true),
                    $"Row {row.Index} must fit its wrapped contents after the column widths settle.");
            }

            void Expand(int offset) => form.GetButton($"expandMessage{offset}Button").PerformClick();

            void AssertBoundedSizing(Action update, string operation)
            {
                MeasuringCell.PreferredSizeCalls = 0;
                update();
                Application.DoEvents();
                // Allow several full passes for scrollbar and column-width changes.
                // The previous code measured all rows after each insertion/style
                // change, resulting in over 21,000 calls for this 57-row message.
                var budget = grid.Rows.Count * grid.Columns.Count * 4;
                Assert.IsTrue(MeasuringCell.PreferredSizeCalls <= budget,
                    $"{operation} made {MeasuringCell.PreferredSizeCalls} size calculations; expected at most {budget}.");
            }
        }
        finally { AppTheme.SetDarkMode(previousTheme); }
    }

    private sealed class MeasuringCell : DataGridViewTextBoxCell
    {
        public static int PreferredSizeCalls;
        public MeasuringCell() { }

        protected override Size GetPreferredSize(Graphics graphics, DataGridViewCellStyle cellStyle, int rowIndex, Size constraintSize)
        {
            PreferredSizeCalls++;
            return base.GetPreferredSize(graphics, cellStyle, rowIndex, constraintSize);
        }
    }

    private sealed class RecreatedLog : RichTextBox
    {
        public void RecreateNativeHandle() => RecreateHandle();
    }
}
