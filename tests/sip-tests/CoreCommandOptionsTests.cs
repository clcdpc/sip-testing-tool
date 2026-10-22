using System.Globalization;
using Clc.Sip.Tests.TestSupport;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Core command options")]
public sealed class CoreCommandOptionsTests
{
    private static readonly DateTime OptionDate = new(2026, 11, 4, 13, 14, 15);
    private const string OptionDateWire = "20261104    131415";

    [TestMethod]
    public void LoginIncludesAlgorithmIdentifiers()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.Login("encoded-user", "encoded-password", "location", "vendor", uidAlgorithm: "A", pwdAlgorithm: "B");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("93ABCNencoded-user|COencoded-password|CPlocation|VPvendor|", SipRequest.Payload(message));
    }

    [TestMethod]
    public void CheckinIncludesAllOptionalFields()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.ItemCheckIn("item", OptionDate, "return", "properties", noBlock: "Y", cancel: "N", terminalPassword: "checkin");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("09Y", message[..3]);
        Assert.AreEqual(OptionDateWire, message.Substring(21, 18));
        Assert.AreEqual("APreturn|AOinstitution|ABitem|ACcheckin|CHproperties|BIN|", SipRequest.VariableFields(message, fixedLength: 39));
    }

    [TestMethod]
    public void RenewByTitleIncludesAllOptionalFields()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.Renew("patron", "pin", OptionDate, itemBarcode: "", title: "title", itemProperties: "properties",
            thirdPartyAllowed: "N", noBlock: "Y", feeAcknowledged: "Y", terminalPassword: "renew");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("29NY", message[..4]);
        Assert.AreEqual(OptionDateWire, message.Substring(22, 18));
        Assert.AreEqual("AOinstitution|AApatron|ADpin|AJtitle|ACrenew|CHproperties|BOY|", SipRequest.VariableFields(message, fixedLength: 40));
    }

    [TestMethod]
    public void RenewWithoutDueDateWritesBlankFixedField()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.Renew("patron", "pin", dueDate: null, itemBarcode: "item");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("29YN", message[..4]);
        Assert.AreEqual(new string(' ', 18), message.Substring(22, 18));
        Assert.AreEqual("AOinstitution|AApatron|ADpin|ABitem|", SipRequest.VariableFields(message, fixedLength: 40));
    }

    [TestMethod]
    public void RenewAllIncludesPasswordAndFeeAcknowledgement()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.RenewAll("patron", "pin", feeAcknowledged: "N", terminalPassword: "renew-all");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("AOinstitution|AApatron|ADpin|ACrenew-all|BON|", SipRequest.VariableFields(message, fixedLength: 20));
    }

    [TestMethod]
    public void ItemInformationIncludesTerminalPassword()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.ItemInformation("item", terminalPassword: "item-info");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("AOinstitution|ABitem|ACitem-info|", SipRequest.VariableFields(message, fixedLength: 20));
    }

    [TestMethod]
    public void PatronInformationIncludesLanguageSummaryAndPassword()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.PatronInformation("patron", "pin", type: "hold", BP: "", BQ: "", terminalPassword: "patron-info", languageCode: "001");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("63001", message[..5]);
        Assert.AreEqual("Y         ", message.Substring(23, 10));
        Assert.AreEqual("AOinstitution|AApatron|ACpatron-info|ADpin|", SipRequest.VariableFields(message, fixedLength: 33));
    }

    [TestMethod]
    public void PatronStatusIncludesLanguageAndPassword()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.PatronStatus("patron", "pin", terminalPassword: "patron-status", languageCode: "002");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("23002", message[..5]);
        Assert.AreEqual("AOinstitution|AApatron|ACpatron-status|ADpin|", SipRequest.VariableFields(message, fixedLength: 23));
    }

    [TestMethod]
    public void EndSessionIncludesTerminalPassword()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.EndSession("patron", "pin", terminalPassword: "end");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("AOinstitution|AApatron|ACend|ADpin|", SipRequest.VariableFields(message, fixedLength: 20));
    }

    [TestMethod]
    public void FeePaidUsesInvariantDecimalWithAllOptionalFields()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            client.FeePaid(1, 2, 12.5, "patron", "pin", currencyType: "EUR", feeIdentifier: "fee", transactionId: "transaction", terminalPassword: "fee-password");
            var message = server.CaptureSingleRequest(client);

            Assert.AreEqual("0102EUR", message.Substring(20, 7));
            Assert.AreEqual("BV12.5|AOinstitution|AApatron|ADpin|ACfee-password|CGfee|BKtransaction|", SipRequest.VariableFields(message, fixedLength: 27));
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    [TestMethod]
    public void NoticeUpdateIncludesDeliveryDetailsAndOptionalFields()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.NotificationUpdate(NoticeStatus.SuccessfulDelivery, OptionDate, NoticeMedium.Email, NotificationType.Overdue,
            "patron", "item", comment: "comment", terminalPassword: "notice");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("4301", message[..4]);
        Assert.AreEqual(OptionDateWire + "004001", message.Substring(22, 24));
        Assert.AreEqual("AOinstitution|AApatron|ABitem|ACnotice|DEcomment|", SipRequest.VariableFields(message, fixedLength: 46));
    }

    [TestMethod]
    public void AcsStatusIncludesStatusPrintWidthAndVersion()
    {
        using var server = new CommandProbe();
        using var client = server.ConnectClient();

        client.AcsStatus(statusCode: "2", maxPrintWidth: 80, protocolVersion: "1.00");
        var message = server.CaptureSingleRequest(client);

        Assert.AreEqual("9920801.00", SipRequest.Payload(message));
    }
}
