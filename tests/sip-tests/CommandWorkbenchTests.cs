using System.Globalization;
using Clc.Sip.Tests.TestSupport;

namespace Clc.Sip.Tests;

[STATestClass]
[TestCategory("Workbench command options")]
public sealed class CommandWorkbenchTests
{
    private static readonly DateTime OptionDate = new(2026, 11, 4, 13, 14, 15);
    private const string OptionDateWire = "20261104    131415";

    [ClassInitialize]
    public static void Initialize(TestContext _) => WinFormsTestEnvironment.Initialize();

    [TestMethod]
    public void CheckinIncludesAllSelectedOptions()
    {
        using var workbench = new WorkbenchFixture();
        ConfigureCheckin(workbench);

        var message = workbench.SubmitAndVerifyLog("Checkin");

        Assert.AreEqual("09Y", message[..3]);
        Assert.AreEqual(OptionDateWire, message.Substring(21, 18));
        Assert.AreEqual("APreturn|AOAO-live|ABitem|ACcheckin|CHcheckin-properties|BIY|", SipRequest.VariableFields(message, fixedLength: 39));
    }

    [TestMethod]
    public void ResetCheckinRestoresDefaultsAndExecutionTime()
    {
        using var workbench = new WorkbenchFixture();
        ConfigureCheckin(workbench);
        workbench.SubmitAndVerifyLog("Checkin");

        workbench.ResetOptions("Checkin", "resetCheckinOptionsButton");
        var before = DateTime.Now;
        var message = workbench.SubmitAndVerifyLog("Checkin");
        var after = DateTime.Now;

        Assert.AreEqual("09N", message[..3]);
        Assert.AreEqual("APAP-live|AOAO-live|ABitem|AC|", SipRequest.VariableFields(message, fixedLength: 39));
        SipRequest.AssertTimestamp(message, offset: 21, before, after);
    }

    [TestMethod]
    public void RenewByTitleIncludesOptionsAndOmitsSharedItem()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetFlag("renewThirdPartyCheckBox", false);
        workbench.SetFlag("renewNoBlockCheckBox", true);
        workbench.SetFlag("renewUseItemCheckBox", false);
        workbench.SetDateOverride("renewDueDatePicker", false);
        workbench.SetText("renewTitleTextBox", "title");
        workbench.SetText("renewPropertiesTextBox", "renew-properties");
        workbench.SetText("renewPasswordTextBox", "renew");
        workbench.SelectValue("renewFeeComboBox", "Yes (Y)");

        var message = workbench.SubmitAndVerifyLog("Renew");

        Assert.AreEqual("29NY", message[..4]);
        Assert.AreEqual(new string(' ', 18), message.Substring(22, 18));
        Assert.AreEqual("AOAO-live|AApatron|ADpin|AJtitle|ACrenew|CHrenew-properties|BOY|", SipRequest.VariableFields(message, fixedLength: 40));
    }

    [TestMethod]
    public void RenewAllIncludesPasswordAndFeeAcknowledgement()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetText("renewAllPasswordTextBox", "renew-all");
        workbench.SelectValue("renewAllFeeComboBox", "No (N)");

        var message = workbench.SubmitAndVerifyLog("Renew all");

        Assert.AreEqual("AOAO-live|AApatron|ADpin|ACrenew-all|BON|", SipRequest.VariableFields(message, fixedLength: 20));
    }

    [TestMethod]
    public void ItemInformationIncludesTerminalPassword()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetText("itemInfoPasswordTextBox", "info");

        var message = workbench.SubmitAndVerifyLog("Item information");

        Assert.AreEqual("AOAO-live|ABitem|ACinfo|", SipRequest.VariableFields(message, fixedLength: 20));
    }

    [TestMethod]
    public void PatronInformationIncludesLanguageAndOmitsClearedRanges()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetText("patronInfoPasswordTextBox", "patron-info");
        workbench.SetText("patronInfoLanguageTextBox", "001");
        workbench.SetText("bpTextBox", "");
        workbench.SetText("bqTextBox", "");

        var message = workbench.SubmitAndVerifyLog("Patron information");

        Assert.AreEqual("63001", message[..5]);
        Assert.AreEqual("AOAO-live|AApatron|ACpatron-info|ADpin|", SipRequest.VariableFields(message, fixedLength: 33));
    }

    [TestMethod]
    public void PatronStatusIncludesLanguageAndTerminalPassword()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetText("patronStatusPasswordTextBox", "patron-status");
        workbench.SetText("patronStatusLanguageTextBox", "002");

        var message = workbench.SubmitAndVerifyLog("Patron status");

        Assert.AreEqual("23002", message[..5]);
        Assert.AreEqual("AOAO-live|AApatron|ACpatron-status|ADpin|", SipRequest.VariableFields(message, fixedLength: 23));
    }

    [TestMethod]
    public void EndSessionIncludesTerminalPassword()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetText("endSessionPasswordTextBox", "end");

        var message = workbench.SubmitAndVerifyLog("End session");

        Assert.AreEqual("AOAO-live|AApatron|ACend|ADpin|", SipRequest.VariableFields(message, fixedLength: 20));
    }

    [TestMethod]
    public void FeePaidIncludesNormalizedCurrencyPasswordAndIdentifiers()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetText("feeTypeTextBox", "1");
        workbench.SetText("paymentTypeTextBox", "2");
        workbench.SetText("amountTextBox", 12.5.ToString(CultureInfo.CurrentCulture));
        workbench.SetText("feeCurrencyTextBox", "eur");
        workbench.SetText("feePasswordTextBox", "fee");
        workbench.SetText("feeIdentifierTextBox", "id");
        workbench.SetText("transactionIdTextBox", "transaction");

        var message = workbench.SubmitAndVerifyLog("Fee paid");

        Assert.AreEqual("0102EUR", message.Substring(20, 7));
        Assert.AreEqual("BV12.5|AOAO-live|AApatron|ADpin|ACfee|CGid|BKtransaction|", SipRequest.VariableFields(message, fixedLength: 27));
    }

    [TestMethod]
    public void NoticeUpdateIncludesDeliveryDetailsCommentAndPassword()
    {
        using var workbench = new WorkbenchFixture();
        workbench.EnterDate("noticeDeliveryDatePicker", OptionDate);
        workbench.SelectValue("noticeStatusComboBox", NoticeStatus.SuccessfulDelivery);
        workbench.SelectValue("noticeMediumComboBox", NoticeMedium.SMS);
        workbench.SelectValue("notificationTypeComboBox", NotificationType.Hold);
        workbench.SetText("noticeCommentTextBox", "delivered");
        workbench.SetText("noticePasswordTextBox", "notice");

        var message = workbench.SubmitAndVerifyLog("Notice update");

        Assert.AreEqual("4301", message[..4]);
        Assert.AreEqual(OptionDateWire + "005003", message.Substring(22, 24));
        Assert.AreEqual("AOAO-live|AApatron|ABitem|ACnotice|DEdelivered|", SipRequest.VariableFields(message, fixedLength: 46));
    }

    [TestMethod]
    public void LoginIncludesAlgorithmChoicesAndSharedCredentials()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SetText("loginUidAlgorithmTextBox", "A");
        workbench.SetText("loginPwdAlgorithmTextBox", "B");

        var message = workbench.SubmitAndVerifyLog("Login");

        Assert.AreEqual("93ABCNuser|COpassword|CPlocation|VPvendor|", SipRequest.Payload(message));
    }

    [TestMethod]
    public void AcsStatusIncludesStatusPrintWidthAndVersion()
    {
        using var workbench = new WorkbenchFixture();
        workbench.SelectValue("acsStatusComboBox", "Out of paper (1)");
        workbench.SetText("acsPrintWidthTextBox", "80");
        workbench.SetText("acsVersionTextBox", "1.00");

        var message = workbench.SubmitAndVerifyLog("ACS Status");

        Assert.AreEqual("9910801.00", SipRequest.Payload(message));
    }

    private static void ConfigureCheckin(WorkbenchFixture workbench)
    {
        workbench.SetFlag("checkinNoBlockCheckBox", true);
        workbench.EnterDate("checkinReturnDatePicker", OptionDate);
        workbench.SetText("checkinLocationTextBox", "return");
        workbench.SetText("checkinPropertiesTextBox", "checkin-properties");
        workbench.SetText("checkinPasswordTextBox", "checkin");
        workbench.SelectValue("checkinCancelComboBox", "Yes (Y)");
    }
}
