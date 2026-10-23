using System.Drawing;
using Clc.Sip.Helpers;
using Clc.Sip.Tests.TestSupport;
using static Clc.Sip.Tests.TestSupport.WinFormsTestEnvironment;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Regression")]
public sealed class PolarisLogFieldTests
{
    private const string patronHeader = "64              00120261022    083815000100000000000600000000";
    private const string checkinHeader = "101YNN20261022    083815";
    private const string statusHeader = "24              00120261022    083815";

    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod(DisplayName = "Documented Polaris fields have names and retain their exact values")]
    [DataRow("64", "U1", "", "User1")]
    [DataRow("64", "U2", "NONE or UNUSED", "User2")]
    [DataRow("64", "U3", "NONE or UNUSED", "User3")]
    [DataRow("64", "U4", "YES Computer Access", "User4")]
    [DataRow("64", "U5", "", "User5")]
    [DataRow("64", "BC", "19890101    000000", "Birth date")]
    [DataRow("64", "PX", "20270124    235959", "Patron expiration date")]
    [DataRow("64", "PY", "N", "Patron expired")]
    [DataRow("64", "PA", "1", "Patron code")]
    [DataRow("64", "PE", "MAIN", "Home branch")]
    [DataRow("64", "PS", "", "Patron statistical class")]
    [DataRow("64", "PZ", "12345", "Postal code")]
    [DataRow("10", "CR", "FIC", "Collection abbreviation")]
    [DataRow("10", "CS", "F AUTHOR", "Call number")]
    [DataRow("10", "CT", "MAIN", "Hold pickup branch")]
    [DataRow("10", "CY", "12345678901234", "Next hold patron barcode")]
    [DataRow("10", "DA", "READER, TEST", "Next hold patron name")]
    public void PolarisFieldsHaveReferenceDescriptions(string messageId, string code, string value, string name)
    {
        var header = messageId == "64" ? patronHeader : checkinHeader;
        var message = header + code + value + "|AY1AZABCD";
        var detail = SipLogFields.DescribeMessage(message).Fields.Single(field => field.Name.StartsWith(code + " · ", StringComparison.Ordinal));
        StringAssert.Contains(detail.Name, name);
        StringAssert.Contains(detail.Name, "Polaris");
        Assert.AreEqual(value, detail.Value);
        Assert.AreEqual(SipLogValueState.Normal, detail.State);
        Assert.AreEqual(code + value, message.Substring(detail.Offset, detail.Length));
        foreach (var character in new[] { 0, 1 })
        {
            var help = SipLogFields.Find("<-- " + message, 4 + header.Length + character);
            Assert.AreEqual(code, help?.Identifier);
            StringAssert.Contains(help.Description, name);
        }
        if (code[0] == 'U')
        {
            StringAssert.Contains(detail.Description, "configured by the library");
            Assert.IsFalse(detail.IsFlag, "User fields are library-defined text, even when their value starts with YES or NONE.");
        }
        if (code == "BC")
        {
            Assert.AreEqual("1989-01-01", detail.Meaning);
        }
    }

    [TestMethod(DisplayName = "Polaris checkin alert codes decode in their message context")]
    [DataRow("  ", "Normal checkin")]
    [DataRow("00", "Unknown alert")]
    [DataRow("01", "Hold for another library")]
    [DataRow("02", "Hold for this library")]
    [DataRow("04", "Send to another library")]
    [DataRow("03", "listed as unused")]
    [DataRow("99", "listed as unused")]
    [DataRow("07", "Unrecognized Polaris alert")]
    public void CheckinAlertCodesHaveSpecificMeanings(string value, string explanation)
    {
        var message = checkinHeader + "CV" + value + "|";
        var detail = SipLogFields.DescribeMessage(message).Fields.Last();
        Assert.AreEqual("CV · Checkin alert type (Polaris)", detail.Name);
        StringAssert.Contains(detail.Meaning, explanation);
        var help = SipLogFields.Find("<-- " + message, 4 + checkinHeader.Length + 2);
        Assert.IsTrue(help.IsFixed);
        Assert.AreEqual(4, help.Length);
        StringAssert.Contains(help.Description, explanation);
    }

    [TestMethod(DisplayName = "Legacy login CV and unknown alphanumeric fields are distinct from checkin alerts")]
    public void FieldContextAndUnknownExtensionsRemainSafe()
    {
        var profile = SipLogFields.DescribeMessage("9300CVENVISIONWARE|").Fields.Last();
        StringAssert.Contains(profile.Name, "Vendor profile");
        StringAssert.Contains(profile.Description, "3.6");
        Assert.AreEqual("ENVISIONWARE", profile.Value);
        var unknown = SipLogFields.DescribeMessage("9901002.00CV02|X9custom|").Fields;
        StringAssert.Contains(unknown[^2].Name, "Unrecognized field");
        Assert.AreEqual("X9 · Unrecognized field", unknown[^1].Name);
        Assert.AreEqual("custom", unknown[^1].Value);
        Assert.AreEqual(SipLogValueState.Normal, unknown[^1].State);
        Assert.AreEqual("X9", SipLogFields.Find("--> 9901002.00X9custom|", 15)?.Identifier);
        var birthText = SipLogFields.DescribeMessage(patronHeader + "BCnot a date|").Fields.Last();
        Assert.AreEqual("BC · Birth date (Polaris)", birthText.Name);
        Assert.AreEqual("not a date", birthText.Value);
        Assert.AreEqual(SipLogValueState.Normal, birthText.State);
        Assert.AreEqual("No birth date supplied.", SipLogFields.DescribeMessage(patronHeader + "BC                  |").Fields.Last().Meaning);
    }

    [TestMethod(DisplayName = "Polaris authentication overrides explain password comparison")]
    [DataRow("AUTHENTICATE", "case-sensitive")]
    [DataRow("AUTHENTICATE_INSENSITIVE", "case-insensitive")]
    public void AuthenticationOverridesExplainTheValue(string value, string explanation)
    {
        var message = "9300CP" + value + "|";
        var detail = SipLogFields.DescribeMessage(message).Fields.Last();
        Assert.AreEqual("CP · Location code", detail.Name);
        StringAssert.Contains(detail.Meaning, explanation);
        StringAssert.Contains(SipLogFields.Find("--> " + message, 8).Description, explanation);
    }

    [TestMethod(DisplayName = "Polaris 8.1 extensions retain wire values and expose qualified definitions in field help")]
    [DataRow("64", "DB", "-25", "Patron credit balance", "Monetary units have not been confirmed")]
    [DataRow("42", "DB", "0", "Patron credit balance", "refreshed patron data")]
    [DataRow("64", "SI", "000123", "Internal patron ID", "EnvisionWare")]
    [DataRow("24", "FA", "17", "Accrued fines total", "truncates")]
    [DataRow("64", "FA", "0.00", "Accrued fines total", "Monetary units have not been confirmed")]
    [DataRow("24", "PC", "000123", "Internal patron ID", "PatronID")]
    [DataRow("64", "PC", "000123", "Internal patron ID", "PatronID")]
    [DataRow("24", "PB", "001234567890", "Resolved patron barcode", "username")]
    [DataRow("64", "PB", "001234567890", "Resolved patron barcode", "username")]
    [DataRow("42", "TX", "0", "Deposit response value", "literal decimal 0")]
    [DataRow("42", "TX", "vendor-specific", "Deposit response value", "business meaning has not been established")]
    public void UpdatedReferenceFieldsPreserveRawValues(string messageId, string identifier, string value, string name, string qualification)
    {
        var header = messageId switch { "64" => patronHeader, "24" => statusHeader, "42" => "42Y", _ => throw new ArgumentException(messageId) };
        var message = header + identifier + value + "|AY1AZABCD";
        var detail = SipLogFields.DescribeMessage(message).Fields.Single(field => field.Name.StartsWith(identifier + " · ", StringComparison.Ordinal));
        StringAssert.Contains(detail.Name, name);
        StringAssert.Contains(detail.Name, "Polaris");
        StringAssert.Contains(detail.Description, qualification);
        Assert.AreEqual(value, detail.Value);
        Assert.AreEqual(value, detail.DisplayValue, "Numeric identifiers, balances and opaque values must not be reformatted.");
        Assert.AreEqual(identifier + value, message.Substring(detail.Offset, detail.Length));
        Assert.AreEqual(SipLogValueState.Normal, detail.State);
        Assert.IsNull(detail.Meaning, "Units and additional business meanings must not be inferred.");
        var help = SipLogFields.Find("<-- " + message, 4 + detail.Offset);
        StringAssert.Contains(help.Description, qualification);
        Assert.AreEqual(identifier, help.Identifier);
        Assert.IsNull(SipLogFields.Find("<-- " + message, 6 + detail.Offset), "Variable field values retain their existing hover behavior.");
    }

    [TestMethod(DisplayName = "Polaris 8.1 fields are only defined in documented reply contexts")]
    public void UpdatedExtensionNamesDoNotLeakIntoOtherMessageTypes()
    {
        foreach (var identifier in new[] { "DB", "SI", "FA", "PC", "PB", "TX" })
        {
            var detail = SipLogFields.DescribeMessage("9300" + identifier + "opaque|").Fields.Last();
            Assert.AreEqual(identifier + " · Unrecognized field", detail.Name);
            Assert.AreEqual("opaque", detail.Value);
        }
        foreach (var identifier in new[] { "DB", "SI", "TX" })
        {
            StringAssert.Contains(SipLogFields.DescribeMessage(statusHeader + identifier + "opaque|").Fields.Last().Name, "Unrecognized field");
        }
        StringAssert.Contains(SipLogFields.DescribeMessage(patronHeader + "TXopaque|").Fields.Last().Name, "Unrecognized field");
    }

    [TestMethod(DisplayName = "Deposit response 42 reads its Y/N header and first AA field without requiring AO")]
    [DataRow("Y", "set (Y)")]
    [DataRow("N", "clear (N)")]
    public void DepositResponseUsesTheDocumentedHeader(string result, string meaning)
    {
        var message = "42" + result + "AArequested-user|DB-25|TX0|";
        var details = SipLogFields.DescribeMessage(message);
        Assert.AreEqual("Deposit-account activity response (Polaris)", details.Name);
        var flag = details.Fields.Single(field => field.Name == "Deposit result (Polaris)");
        StringAssert.Contains(flag.Meaning, meaning);
        Assert.AreEqual(2, flag.Offset);
        Assert.AreEqual(1, flag.Length);
        var patron = details.Fields.Single(field => field.Name == "AA · Patron identifier");
        Assert.AreEqual(3, patron.Offset);
        Assert.AreEqual("requested-user", patron.Value);
        StringAssert.Contains(patron.Description, "Copied");
        Assert.IsFalse(details.Fields.Any(field => field.Name.Contains("Unrecognized", StringComparison.Ordinal)));
        var help = SipLogFields.Find("<-- " + message, 6);
        Assert.IsTrue(help.IsFixed);
        Assert.AreEqual(1, help.Length);
        StringAssert.Contains(help.Description, meaning);
    }

    [TestMethod(DisplayName = "Notice response 44 preserves unproven result codes and describes BK as an echo")]
    [DataRow("Y")]
    [DataRow("N")]
    [DataRow("1")]
    [DataRow("0")]
    [DataRow("?")]
    public void NoticeResponseDoesNotInventResultOrTransactionSemantics(string result)
    {
        var message = "44" + result + "AOlibrary|AApatron|BKrequest-reference|ABitem|";
        var details = SipLogFields.DescribeMessage(message);
        Assert.AreEqual("Notice update response (Polaris)", details.Name);
        var flag = details.Fields.Single(field => field.Name == "Notice result (Polaris)");
        Assert.AreEqual(result, flag.Value);
        Assert.IsNull(flag.Meaning, "The reference does not establish response 44's result-code meanings.");
        var echo = details.Fields.Single(field => field.Name.StartsWith("BK · ", StringComparison.Ordinal));
        Assert.AreEqual("request-reference", echo.Value);
        StringAssert.Contains(echo.Name, "Echoed request value");
        StringAssert.Contains(echo.Description, "unchanged");
        StringAssert.Contains(echo.Description, "business meaning is unconfirmed");
        StringAssert.Contains(SipLogFields.Find("<-- " + message, 4 + echo.Offset).Description, "unchanged");
        Assert.AreEqual("AO · Institution ID", details.Fields[2].Name, "The first identified field must not be swallowed as an unknown header.");
    }

    [TestMethod(DisplayName = "BK retains standard meaning outside the proprietary notice pair")]
    public void NoticeRequestAndStandardTransactionIdRemainDistinct()
    {
        var notice = SipLogFields.DescribeMessage("430120261022    08381520261022    083800004003BKrequest-reference|").Fields.Last();
        StringAssert.Contains(notice.Name, "Notice request value");
        StringAssert.Contains(notice.Description, "unchanged");
        var payment = SipLogFields.DescribeMessage("3720261022    0838150400USDBKpayment-reference|").Fields.Last();
        Assert.AreEqual("BK · Transaction ID", payment.Name);
    }

    [TestMethod(DisplayName = "Polaris expiration flags preserve processing and end-of-day qualifications")]
    [DataRow("Y", "when evaluated")]
    [DataRow("N", "may have been skipped")]
    public void ExpirationFlagDoesNotClaimAnUnevaluatedPatronIsCurrent(string value, string qualification)
    {
        var message = patronHeader + "PY" + value + "|";
        var field = SipLogFields.DescribeMessage(message).Fields.Last();
        StringAssert.Contains(field.Description, "23:59:59");
        StringAssert.Contains(field.Description, "EnvisionWare");
        StringAssert.Contains(field.Meaning, qualification);
        StringAssert.Contains(SipLogFields.Find("<-- " + message, 6 + patronHeader.Length).Description, qualification);
    }

    [TestMethod(DisplayName = "Incomplete proprietary response headers and request 41 keep their unknown data intact")]
    public void IncompleteOrUnprovenProprietaryHeadersAreNotGuessed()
    {
        foreach (var message in new[] { "42", "44" })
        {
            var field = SipLogFields.DescribeMessage(message).Fields.Last();
            Assert.AreEqual("Incomplete fixed header", field.Name);
            Assert.AreEqual(SipLogValueState.Invalid, field.State);
        }
        var request = SipLogFields.DescribeMessage("41opaque positional data|AApatron|");
        Assert.AreEqual("Unknown message (41)", request.Name);
        Assert.AreEqual("opaque positional data", request.Fields.Single(field => field.Name == "Unrecognized header").Value);
    }

    [TestMethod(DisplayName = "Repeated default AV values split into transaction ID and outstanding amount")]
    public void DefaultFineEntriesHaveSeparateComponents()
    {
        var message = patronHeader + "AV25116288 $5.00|AV25116287 $15.89|AY1AZ81AC";
        var entries = SipLogFields.DescribeMessage(message).Fields.Where(field => field.Name.StartsWith("AV · ")).ToArray();
        Assert.AreEqual(2, entries.Length);
        CollectionAssert.AreEqual(new[] { "25116288", "$5.00" }, entries[0].Children.Select(child => child.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "25116287", "$15.89" }, entries[1].Children.Select(child => child.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "Fine transaction ID", "Outstanding amount" }, entries[0].Children.Select(child => child.Name).ToArray());
        foreach (var entry in entries)
        {
            AssertPartOffsets(message, entry);
            var help = SipLogFields.Find("<-- " + message, entry.Offset + 4);
            StringAssert.Contains(help.Description, "Fine transaction ID: " + entry.Children[0].Value);
            StringAssert.Contains(help.Description, "Outstanding amount: " + entry.Children[1].Value);
            Assert.IsNull(SipLogFields.Find("<-- " + message, entry.Offset + 6), "AV remains variable data in the raw transcript.");
        }
    }

    [TestMethod(DisplayName = "Detailed AV values preserve quoted fee descriptions, item barcodes and titles")]
    public void DetailedFineEntryHasEveryDocumentedComponent()
    {
        const string value = "000123 $15.89 \"Charge Overdue fine with spaces\" ITEM123 A title with \"quotes\" and spaces";
        var message = patronHeader + "AV" + value + "|";
        var entry = SipLogFields.DescribeMessage(message).Fields.Last();
        CollectionAssert.AreEqual(new[] { "000123", "$15.89", "Charge", "Overdue fine with spaces", "ITEM123", "A title with \"quotes\" and spaces" },
            entry.Children.Select(child => child.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "Fine transaction ID", "Outstanding amount", "Fine type", "Fee description", "Related item barcode", "Related item title" },
            entry.Children.Select(child => child.Name).ToArray());
        Assert.AreEqual(value, entry.Value);
        AssertPartOffsets(message, entry);
    }

    [TestMethod(DisplayName = "Amount-first AV keeps the combined description and title together")]
    [DataRow("$5.00 Charge Overdue fine for a book")]
    [DataRow("5.00 Charge Overdue fine for a book")]
    public void AmountFirstFineEntryDoesNotInventATransactionId(string value)
    {
        var message = patronHeader + "AV" + value + "|";
        var entry = SipLogFields.DescribeMessage(message).Fields.Last();
        CollectionAssert.AreEqual(new[] { "Outstanding amount", "Fine type", "Fee description / title" }, entry.Children.Select(child => child.Name).ToArray());
        Assert.AreEqual("Overdue fine for a book", entry.Children[^1].Value);
        StringAssert.Contains(entry.Description, "post-4.1");
        AssertPartOffsets(message, entry);
    }

    [TestMethod(DisplayName = "Fine amounts retain their exact formatting")]
    [DataRow("$0.00")]
    [DataRow("$-1.25")]
    [DataRow("$1,234.56")]
    public void FineAmountsKeepCurrencyAndSigns(string amount)
    {
        var entry = SipLogFields.DescribeMessage(patronHeader + "AV000123 " + amount + "|").Fields.Last();
        Assert.AreEqual("000123", entry.Children[0].Value);
        Assert.AreEqual(amount, entry.Children[1].DisplayValue);
    }

    [TestMethod(DisplayName = "Other AV layouts remain opaque rather than being assigned invented meanings")]
    [DataRow("")]
    [DataRow("opaque server data")]
    [DataRow("ITEM123 $5.00")]
    [DataRow("123 5.00 Charge Other format")]
    public void UnrecognizedFineLayoutsRemainIntact(string value)
    {
        var entry = SipLogFields.DescribeMessage(patronHeader + "AV" + value + "|").Fields.Last();
        Assert.AreEqual(value, entry.Value);
        Assert.IsNull(entry.Children);
        Assert.AreEqual(SipLogValueState.Normal, entry.State);
    }

    [TestMethod(DisplayName = "Unrecognized fine detail tails are preserved as additional data")]
    public void PartialFineDetailsAreNotLost()
    {
        const string tail = "\"Charge Unclosed fee description";
        var message = patronHeader + "AV123 $5.00 " + tail + "|";
        var entry = SipLogFields.DescribeMessage(message).Fields.Last();
        Assert.AreEqual(3, entry.Children.Count);
        Assert.AreEqual("Additional fine data", entry.Children[^1].Name);
        Assert.AreEqual(tail, entry.Children[^1].Value);
        AssertPartOffsets(message, entry);
    }

    [TestMethod(DisplayName = "Clear names replace redundant count and identity descriptions")]
    public void DescriptionsAddMeaningInsteadOfRepeatingTheName()
    {
        var details = SipLogFields.DescribeMessage(patronHeader + "AEReader|AMLibrary|BDAddress|BEreader@example.test|BF5550101234|");
        var counts = details.Fields.Where(field => field.Name.EndsWith(" count", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(6, counts.Length);
        Assert.IsTrue(counts.All(field => field.Description == ""));
        Assert.AreEqual("1 item.", counts[0].Meaning);
        Assert.AreEqual("6 items.", counts[3].Meaning);
        Assert.IsTrue(details.Fields.TakeLast(5).All(field => field.Description == ""));
        var help = SipLogFields.Find("<-- " + patronHeader, 4 + counts[0].Offset);
        StringAssert.Contains(help.Description, "Hold items count");
        Assert.IsFalse(help.Description.Contains("\n\n", StringComparison.Ordinal));
        var blankCount = SipLogFields.DescribeMessage("661        20261022    083815").Fields[2];
        Assert.AreEqual("Count unavailable.", blankCount.Meaning);
    }

    [TestMethod(DisplayName = "Expanded vendor rows show component values, clear counts and library-defined user fields in both themes")]
    [DataRow(false)]
    [DataRow(true)]
    public void ExpandedVendorComponentsRemainReadableAndCopyable(bool darkMode)
    {
        var previousTheme = AppTheme.IsDarkMode;
        try
        {
            AppTheme.SetDarkMode(darkMode);
            using var form = new MainForm { ClientSize = new Size(1280, 1000) };
            ShowOffscreen(form);
            form.LogOutput(new SipTransaction
            {
                Message = "6300120261022    083815   Y      AO123|AA12345678901234|BP1|BQ5|",
                Response = patronHeader + "U1|U2NONE or UNUSED|U3NONE or UNUSED|U4YES Computer Access|U5|" +
                    "AV25116288 $5.00|AV123 $15.89 \"Charge Overdue fine\" ITEM123 Example item title|AV5.00 Charge Fee description and title|AY1AZ81AC"
            });
            Application.DoEvents();
            var log = form.GetRichTextBox("logRichTextBox");
            log.Select(4, 3);
            var text = log.Text;
            var rtf = log.Rtf;
            var responseOffset = text.IndexOf("<--", StringComparison.Ordinal);
            form.GetButton($"expandMessage{responseOffset}Button").PerformClick();
            Application.DoEvents();
            var grid = form.GetDataGridView("messageFieldsGrid");
            var amount = Rows("Outstanding amount").First();
            Assert.AreEqual("$5.00", amount.Cells[2].Value);
            Assert.AreEqual("Overdue fine", Rows("Fee description").Single().Cells[2].Value);
            Assert.AreEqual("Example item title", Rows("Related item title").Single().Cells[2].Value);
            Assert.AreEqual("Fee description and title", Rows("Fee description / title").Single().Cells[2].Value);
            Assert.AreEqual("(empty)", Rows("U1 · User1 (Polaris)").Single().Cells[2].Value);
            Assert.AreEqual("YES Computer Access", Rows("U4 · User4 (Polaris)").Single().Cells[2].Value);
            Assert.AreEqual("", Rows("Hold items count").Single().Cells[1].Value);
            Assert.AreEqual("Not set (space)", Rows("Flag 4 (position 3)").Single().Cells[2].Value);
            Assert.AreEqual(rtf, log.Rtf);
            var firstFine = Rows("AV · Fine item details").First();
            grid.FirstDisplayedScrollingRowIndex = Rows("Hold items count").Single().Index;
            SavePreview(darkMode ? "polaris-patron-fields-dark.png" : "polaris-patron-fields-light.png");
            grid.FirstDisplayedScrollingRowIndex = firstFine.Index;
            SavePreview(darkMode ? "polaris-fields-dark.png" : "polaris-fields-light.png");
            amount.Cells[2].Selected = true;
            StringAssert.Contains(grid.GetClipboardContent().GetText(), "$5.00");
            grid.ClearSelection();
            AppTheme.SetDarkMode(!darkMode);
            Application.DoEvents();
            Assert.AreEqual("$5.00", amount.Cells[2].Value);
            Assert.AreEqual(AppTheme.Surface, firstFine.DefaultCellStyle.BackColor);
            Assert.AreEqual(text, log.Text);
            Assert.AreEqual(4, log.SelectionStart);
            Assert.AreEqual(3, log.SelectionLength);

            IEnumerable<DataGridViewRow> Rows(string name) => grid.Rows.Cast<DataGridViewRow>().Where(row => ((SipLogDetail)row.Tag).Name == name);

            void SavePreview(string filename)
            {
                Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, filename));
            }
        }
        finally { AppTheme.SetDarkMode(previousTheme); }
    }

    private static void AssertPartOffsets(string message, SipLogDetail entry)
    {
        foreach (var child in entry.Children)
        {
            Assert.AreEqual(child.Value, message.Substring(child.Offset, child.Length));
            Assert.IsFalse(child.IsFlag);
            Assert.AreEqual(child.Value, child.DisplayValue);
        }
    }
}
