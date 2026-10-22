using System.Globalization;
using Clc.Sip.Tests.TestSupport;
using Clc.Sip;

namespace Clc.Sip.Tests;

public sealed partial class FormSubmissionTests
{
    private const string SubmittedDateText = "2027-01-02 03:04:05";

    private const string SubmittedDateWire = "20270102    030405";

    private static readonly string BlankDateWire = new(' ', 18);

    // Expected packet fields are independent literals. Each case changes the actual
    // form controls, clicks a public Button.PerformClick(), and checks bytes captured
    // by a TCP peer. No command handler or SIP method is called by this matrix.
    private sealed record SubmissionSpec(string ResetPrefix, int VariablesOffset, string Header, string Variables);

    private static readonly Dictionary<string, SubmissionSpec> submissionDefaults = new()
    {
        ["Checkout"] = new("checkout", 40, "11YN", "AOAO-live|AApatron|ABitem|AC|ADpin|BON|"),
        ["Checkin"] = new("checkin", 39, "09N", "APAP-live|AOAO-live|ABitem|AC|"),
        ["Renew"] = new("renew", 40, "29YN", "AOAO-live|AApatron|ADpin|ABitem|"),
        ["Renew all"] = new("renewAll", 20, "65", "AOAO-live|AApatron|ADpin|"),
        ["Item information"] = new("itemInfo", 20, "17", "AOAO-live|ABitem|AC|"),
        ["Patron information"] = new("patronInfo", 33, "63000", "AOAO-live|AApatron|ADpin|BP1|BQ5|"),
        ["Patron status"] = new("patronStatus", 23, "23000", "AOAO-live|AApatron|AC|ADpin|"),
        ["End session"] = new("endSession", 20, "35", "AOAO-live|AApatron|ADpin|"),
        ["Fee paid"] = new("fee", 27, "37", "BV12.5|AOAO-live|AApatron|ADpin|"),
        ["Notice update"] = new("notice", 46, "4300", "AOAO-live|AApatron|ABitem|AC|"),
        ["Login"] = new("login", 4, "9300", "CNuser|COpassword|CPlocation|VPvendor|"),
        ["ACS Status"] = new("acs", 10, "9901002.00", "")
    };

    private sealed record SubmissionCase(string Command, string Label, Action<MainForm> Edit,
        string Header = null, string Variables = null, string Date = null, string Summary = null, string FixedTail = null, bool RibbonLogin = false, string Culture = null);

    // Discovery contains only serializable command and option names.
    private static IEnumerable<SubmissionCase> AllSubmissionCases() =>
        GetCheckoutSubmissionCases()
            .Concat(GetCheckinSubmissionCases())
            .Concat(GetRenewalSubmissionCases())
            .Concat(GetPatronSubmissionCases())
            .Concat(GetFeeSubmissionCases())
            .Concat(GetNoticeSubmissionCases())
            .Concat(GetLoginStatusSubmissionCases());

    private static IReadOnlyList<SubmissionCase> GetCheckoutSubmissionCases()
    {
        var cases = new List<SubmissionCase>
        {
            new("Checkout", "defaults", null),
            new("Checkout", "renewals disabled", form => SetFlag(form, "checkoutRenewalCheckBox", false), Header: "11NN"),
            new("Checkout", "no block enabled", form => SetFlag(form, "checkoutNoBlockCheckBox", true), Header: "11YY"),
            new("Checkout", "typed date and time", form => SetDateText(form, "checkoutDueDatePicker", true), Date: SubmittedDateWire),
            new("Checkout", "unchecked invalid date is blank", form => SetDateText(form, "checkoutDueDatePicker", false, "2026-02-30 00:00:00"), Date: BlankDateWire),
            new("Checkout", "item properties", form => SetText(form, "checkoutItemPropertiesTextBox", "checkout-properties"),
                Variables: "AOAO-live|AApatron|ABitem|AC|ADpin|CHcheckout-properties|BON|"),
            new("Checkout", "terminal password", form => SetText(form, "checkoutTerminalPasswordTextBox", "checkout-password"),
                Variables: "AOAO-live|AApatron|ABitem|ACcheckout-password|ADpin|BON|"),
            new("Checkout", "updated shared patron, PIN and item", form =>
            {
                SetText(form, "patronBarcodeTextBox", "edited-patron");
                SetText(form, "patronPinTextBox", "edited-pin");
                SetText(form, "itemBarcodeTextBox", "edited-item");
            },
                Variables: "AOAO-live|AAedited-patron|ABedited-item|AC|ADedited-pin|BON|")
        };
        foreach (var (index, suffix) in new[] { (0, ""), (1, "BON|"), (2, "BOY|") })
        {
            cases.Add(new("Checkout", "fee acknowledgement " + index, form => SetChoice(form, "checkoutFeeAcknowledgedComboBox", index),
                Variables: "AOAO-live|AApatron|ABitem|AC|ADpin|" + suffix));
        }

        foreach (var (index, suffix) in new[] { (0, ""), (1, "BIN|"), (2, "BIY|") })
        {
            cases.Add(new("Checkout", "cancel " + index, form => SetChoice(form, "checkoutCancelComboBox", index), Variables: "AOAO-live|AApatron|ABitem|AC|ADpin|BON|" + suffix));
        }

        return cases;
    }

    private static IReadOnlyList<SubmissionCase> GetCheckinSubmissionCases()
    {
        var cases = new List<SubmissionCase>
        {
            new("Checkin", "defaults and automatic return time", null),
            new("Checkin", "no block enabled", form => SetFlag(form, "checkinNoBlockCheckBox", true), Header: "09Y"),
            new("Checkin", "typed return date and time", form => SetDateText(form, "checkinReturnDatePicker", true), Date: SubmittedDateWire),
            new("Checkin", "unchecked invalid return date uses now", form => SetDateText(form, "checkinReturnDatePicker", false, "2026-02-30 00:00:00")),
            new("Checkin", "return location override", form => SetText(form, "checkinLocationTextBox", "returns"), Variables: "APreturns|AOAO-live|ABitem|AC|"),
            new("Checkin", "edited shared terminal fallback", form => SetText(form, "apTextBox", "edited-terminal"), Variables: "APedited-terminal|AOAO-live|ABitem|AC|"),
            new("Checkin", "override wins over edited terminal", form =>
            {
                SetText(form, "apTextBox", "edited-terminal");
                SetText(form, "checkinLocationTextBox", "returns");
            },
                Variables: "APreturns|AOAO-live|ABitem|AC|"),
            new("Checkin", "item properties", form => SetText(form, "checkinPropertiesTextBox", "checkin-properties"),
                Variables: "APAP-live|AOAO-live|ABitem|AC|CHcheckin-properties|"),
            new("Checkin", "terminal password", form => SetText(form, "checkinPasswordTextBox", "checkin-password"), Variables: "APAP-live|AOAO-live|ABitem|ACcheckin-password|")
        };
        foreach (var (index, suffix) in new[] { (0, ""), (1, "BIN|"), (2, "BIY|") })
        {
            cases.Add(new("Checkin", "cancel " + index, form => SetChoice(form, "checkinCancelComboBox", index), Variables: "APAP-live|AOAO-live|ABitem|AC|" + suffix));
        }

        return cases;
    }

    private static IReadOnlyList<SubmissionCase> GetRenewalSubmissionCases()
    {
        var cases = new List<SubmissionCase>
        {
            new("Renew", "defaults", null),
            new("Renew", "third-party renewals disabled", form => SetFlag(form, "renewThirdPartyCheckBox", false), Header: "29NN"),
            new("Renew", "no block enabled", form => SetFlag(form, "renewNoBlockCheckBox", true), Header: "29YY"),
            new("Renew", "typed due date and time", form => SetDateText(form, "renewDueDatePicker", true), Date: SubmittedDateWire),
            new("Renew", "unchecked due date is blank", form => SetDateText(form, "renewDueDatePicker", false), Date: BlankDateWire),
            new("Renew", "unchecked invalid due date is blank", form => SetDateText(form, "renewDueDatePicker", false, "2026-02-30 00:00:00"), Date: BlankDateWire),
            new("Renew", "omit shared item barcode", form => SetFlag(form, "renewUseItemCheckBox", false), Variables: "AOAO-live|AApatron|ADpin|"),
            new("Renew", "title alongside shared item", form => SetText(form, "renewTitleTextBox", "title-id"), Variables: "AOAO-live|AApatron|ADpin|ABitem|AJtitle-id|"),
            new("Renew", "title without shared item", form =>
            {
                SetFlag(form, "renewUseItemCheckBox", false);
                SetText(form, "renewTitleTextBox", "title-id");
            },
                Variables: "AOAO-live|AApatron|ADpin|AJtitle-id|"),
            new("Renew", "item properties", form => SetText(form, "renewPropertiesTextBox", "renew-properties"), Variables: "AOAO-live|AApatron|ADpin|ABitem|CHrenew-properties|"),
            new("Renew", "terminal password", form => SetText(form, "renewPasswordTextBox", "renew-password"), Variables: "AOAO-live|AApatron|ADpin|ABitem|ACrenew-password|"),
            new("Renew all", "defaults", null),
            new("Renew all", "terminal password", form => SetText(form, "renewAllPasswordTextBox", "renew-all-password"),
                Variables: "AOAO-live|AApatron|ADpin|ACrenew-all-password|")
        };
        foreach (var (index, suffix) in new[] { (0, ""), (1, "BON|"), (2, "BOY|") })
        {
            cases.Add(new("Renew", "fee acknowledgement " + index, form => SetChoice(form, "renewFeeComboBox", index), Variables: "AOAO-live|AApatron|ADpin|ABitem|" + suffix));
            cases.Add(new("Renew all", "fee acknowledgement " + index, form => SetChoice(form, "renewAllFeeComboBox", index), Variables: "AOAO-live|AApatron|ADpin|" + suffix));
        }
        return cases;
    }

    private static IReadOnlyList<SubmissionCase> GetPatronSubmissionCases()
    {
        var cases = new List<SubmissionCase>
        {
            new("Patron information", "defaults", null),
            new("Patron information", "language", form => SetText(form, "patronInfoLanguageTextBox", "002"), Header: "63002"),
            new("Patron information", "terminal password", form => SetText(form, "patronInfoPasswordTextBox", "info-password"),
                Variables: "AOAO-live|AApatron|ACinfo-password|ADpin|BP1|BQ5|"),
            new("Patron information", "start item changed with no summary", form => SetText(form, "bpTextBox", "2"), Variables: "AOAO-live|AApatron|ADpin|BP2|BQ5|"),
            new("Patron information", "end item changed with no summary", form => SetText(form, "bqTextBox", "8"), Variables: "AOAO-live|AApatron|ADpin|BP1|BQ8|"),
            new("Patron information", "omit start only", form => SetText(form, "bpTextBox", ""), Variables: "AOAO-live|AApatron|ADpin|BQ5|"),
            new("Patron information", "omit end only", form => SetText(form, "bqTextBox", ""), Variables: "AOAO-live|AApatron|ADpin|BP1|"),
            new("Patron information", "omit both ranges", form =>
            {
                SetText(form, "bpTextBox", "");
                SetText(form, "bqTextBox", "");
            }, Variables: "AOAO-live|AApatron|ADpin|"),
            new("Patron status", "defaults", null),
            new("Patron status", "language", form => SetText(form, "patronStatusLanguageTextBox", "001"), Header: "23001"),
            new("Patron status", "terminal password", form => SetText(form, "patronStatusPasswordTextBox", "status-password"),
                Variables: "AOAO-live|AApatron|ACstatus-password|ADpin|"),
            new("End session", "defaults", null),
            new("End session", "terminal password", form => SetText(form, "endSessionPasswordTextBox", "end-password"), Variables: "AOAO-live|AApatron|ACend-password|ADpin|"),
            new("Item information", "defaults", null),
            new("Item information", "terminal password", form => SetText(form, "itemInfoPasswordTextBox", "item-password"), Variables: "AOAO-live|ABitem|ACitem-password|")
        };
        foreach (var (index, summary) in new[] { (0, "          "), (1, "Y         "), (2, " Y        "), (3, "  Y       "), (4, "   Y      "), (5, "    Y     "), (6, "     Y    ") })
        {
            cases.Add(new("Patron information", "summary " + index, form => SetChoice(form, "patronSummaryComboBox", index), Summary: summary));
        }

        return cases;
    }

    private static IReadOnlyList<SubmissionCase> GetFeeSubmissionCases()
    {
        var cases = new List<SubmissionCase>
        {
            new("Fee paid", "defaults with valid required fields", null),
            new("Fee paid", "fee type zero", form => SetText(form, "feeTypeTextBox", "0"), FixedTail: "0002USD"),
            new("Fee paid", "fee type maximum", form => SetText(form, "feeTypeTextBox", "99"), FixedTail: "9902USD"),
            new("Fee paid", "payment type zero", form => SetText(form, "paymentTypeTextBox", "0"), FixedTail: "0100USD"),
            new("Fee paid", "payment type maximum", form => SetText(form, "paymentTypeTextBox", "99"), FixedTail: "0199USD"),
            new("Fee paid", "zero amount", form => SetText(form, "amountTextBox", "0"), Variables: "BV0|AOAO-live|AApatron|ADpin|"),
            new("Fee paid", "fractional amount", form => SetText(form, "amountTextBox", 7.25.ToString(CultureInfo.CurrentCulture)), Variables: "BV7.25|AOAO-live|AApatron|ADpin|"),
            new("Fee paid", "currency normalization", form => SetText(form, "feeCurrencyTextBox", "eur"), FixedTail: "0102EUR"),
            new("Fee paid", "terminal password", form => SetText(form, "feePasswordTextBox", "fee-password"), Variables: "BV12.5|AOAO-live|AApatron|ADpin|ACfee-password|"),
            new("Fee paid", "fee identifier", form => SetText(form, "feeIdentifierTextBox", "fee-id"), Variables: "BV12.5|AOAO-live|AApatron|ADpin|CGfee-id|"),
            new("Fee paid", "transaction ID", form => SetText(form, "transactionIdTextBox", "transaction-id"), Variables: "BV12.5|AOAO-live|AApatron|ADpin|BKtransaction-id|")
        };
        cases.Add(new("Fee paid", "comma decimal UI to decimal point packet", form => SetText(form, "amountTextBox", "7,25"),
            Variables: "BV7.25|AOAO-live|AApatron|ADpin|", Culture: "de-DE"));
        return cases;
    }

    private static IReadOnlyList<SubmissionCase> GetNoticeSubmissionCases()
    {
        var cases = new List<SubmissionCase>
        {
            new("Notice update", "defaults and automatic delivery time", null),
            new("Notice update", "typed delivery date and time", form => SetDateText(form, "noticeDeliveryDatePicker", true), Date: SubmittedDateWire),
            new("Notice update", "unchecked invalid date uses now", form => SetDateText(form, "noticeDeliveryDatePicker", false, "2026-02-30 00:00:00")),
            new("Notice update", "comment", form => SetText(form, "noticeCommentTextBox", "notice-comment"), Variables: "AOAO-live|AApatron|ABitem|AC|DEnotice-comment|"),
            new("Notice update", "terminal password", form => SetText(form, "noticePasswordTextBox", "notice-password"), Variables: "AOAO-live|AApatron|ABitem|ACnotice-password|")
        };
        foreach (var (index, header) in new[] { (0, "4300"), (1, "4301"), (2, "4302") })
        {
            cases.Add(new("Notice update", "delivery status " + index, form => SetChoice(form, "noticeStatusComboBox", index), Header: header));
        }

        foreach (var (index, tail) in new[] { (0, "000000"), (1, "001000"), (2, "002000"), (3, "003000"), (4, "004000"), (5, "005000") })
        {
            cases.Add(new("Notice update", "delivery medium " + index, form => SetChoice(form, "noticeMediumComboBox", index), FixedTail: tail));
        }

        foreach (var (index, tail) in new[] { (0, "000000"), (1, "000001"), (2, "000002"), (3, "000003"), (4, "000004") })
        {
            cases.Add(new("Notice update", "notice type " + index, form => SetChoice(form, "notificationTypeComboBox", index), FixedTail: tail));
        }

        return cases;
    }

    private static IReadOnlyList<SubmissionCase> GetLoginStatusSubmissionCases()
    {
        var cases = new List<SubmissionCase>
        {
            new("Login", "defaults", null),
            new("Login", "user ID algorithm", form => SetText(form, "loginUidAlgorithmTextBox", "A"), Header: "93A0"),
            new("Login", "password algorithm", form => SetText(form, "loginPwdAlgorithmTextBox", "B"), Header: "930B"),
            new("Login", "ribbon login button uses algorithm options", form =>
            {
                SetText(form, "loginUidAlgorithmTextBox", "A");
                SetText(form, "loginPwdAlgorithmTextBox", "B");
            },
                Header: "93AB", RibbonLogin: true),
            new("Login", "edited username", form => SetText(form, "usernameTextBox", "edited-user"), Variables: "CNedited-user|COpassword|CPlocation|VPvendor|"),
            new("Login", "edited password", form => SetText(form, "passwordTextBox", "edited-password"), Variables: "CNuser|COedited-password|CPlocation|VPvendor|"),
            new("Login", "edited location", form => SetText(form, "locationCodeTextBox", "edited-location"), Variables: "CNuser|COpassword|CPedited-location|VPvendor|"),
            new("Login", "empty location", form => SetText(form, "locationCodeTextBox", ""), Variables: "CNuser|COpassword|CP|VPvendor|"),
            new("Login", "edited vendor profile", form => form.GetComboBox("vendorProfileComboBox").Text = "edited-vendor",
                Variables: "CNuser|COpassword|CPlocation|VPedited-vendor|"),
            new("Login", "omit vendor profile", form => form.GetComboBox("vendorProfileComboBox").Text = "", Variables: "CNuser|COpassword|CPlocation|"),
            new("ACS Status", "defaults", null),
            new("ACS Status", "print width zero", form => SetText(form, "acsPrintWidthTextBox", "0"), Header: "9900002.00"),
            new("ACS Status", "print width padded", form => SetText(form, "acsPrintWidthTextBox", "7"), Header: "9900072.00"),
            new("ACS Status", "print width maximum", form => SetText(form, "acsPrintWidthTextBox", "999"), Header: "9909992.00"),
            new("ACS Status", "protocol version", form => SetText(form, "acsVersionTextBox", "1.00"), Header: "9901001.00")
        };
        foreach (var (index, header) in new[] { (0, "9901002.00"), (1, "9911002.00"), (2, "9921002.00") })
        {
            cases.Add(new("ACS Status", "self-check status " + index, form => SetChoice(form, "acsStatusComboBox", index), Header: header));
        }

        return cases;
    }
}
