using System.Globalization;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.Tests.TestSupport;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;
using static Clc.Sip.Desktop.Tests.TestSupport.SipServer;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
[TestCategory("Avalonia")]
public sealed class CommandOptionsTests
{
    public sealed record OptionCase(SipCommand Command, string Name, Action<CommandOptions> Edit, string[] Contains, string[]? Omits = null)
    {
        public override string ToString() => Command + ": " + Name;
    }

    public static IEnumerable<object[]> Cases()
    {
        static object[] Case(SipCommand command, string name, Action<CommandOptions> edit, string[] contains, string[]? omits = null)
            => [new OptionCase(command, name, edit, contains, omits)];
        yield return Case(SipCommand.Checkout, "renewal and no-block flags", o => { o.RenewalAllowed = false; o.NoBlock = true; }, ["11NY"]);
        yield return Case(SipCommand.Checkout, "date override", o => { o.UseDate = true; o.DateText = "2026-11-30 14:23:45"; }, ["20261130    142345"]);
        yield return Case(SipCommand.Checkout, "date omitted", o => { o.UseDate = false; o.DateText = "invalid"; }, ["                  AO"]);
        yield return Case(SipCommand.Checkout, "properties", o => o.ItemProperties = "checkout-properties", ["CHcheckout-properties|"]);
        yield return Case(SipCommand.Checkout, "fee accepted", o => o.FeeAcknowledged = "Y", ["BOY|"]);
        yield return Case(SipCommand.Checkout, "fee omitted", o => o.FeeAcknowledged = "", [], ["BO"]);
        yield return Case(SipCommand.Checkout, "cancel no", o => o.Cancel = "N", ["BIN|"]);
        yield return Case(SipCommand.Checkin, "no block", o => o.NoBlock = true, ["09Y"]);
        yield return Case(SipCommand.Checkin, "return date", o => { o.UseDate = true; o.DateText = "2026-11-30 14:23:45"; }, ["20261130    142345"]);
        yield return Case(SipCommand.Checkin, "AP override", o => o.ReturnLocation = "return-desk", ["APreturn-desk|"]);
        yield return Case(SipCommand.Checkin, "shared AP fallback", o => o.ReturnLocation = "", ["APdesk-1|"]);
        yield return Case(SipCommand.Checkin, "properties and cancel", o => { o.ItemProperties = "return-properties"; o.Cancel = "Y"; }, ["CHreturn-properties|", "BIY|"]);
        yield return Case(SipCommand.Renew, "third party and no block", o => { o.ThirdPartyAllowed = false; o.NoBlock = true; }, ["29NY"]);
        yield return Case(SipCommand.Renew, "due date", o => { o.UseDate = true; o.DateText = "2026-11-30 14:23:45"; }, ["20261130    142345"]);
        yield return Case(SipCommand.Renew, "blank date", o => { o.UseDate = false; o.DateText = "invalid"; }, ["                  AO"]);
        yield return Case(SipCommand.Renew, "by title", o => { o.IncludeItemBarcode = false; o.TitleIdentifier = "title-123"; }, ["AJtitle-123|"], ["ABI456|"]);
        yield return Case(SipCommand.Renew, "properties and fee no", o => { o.ItemProperties = "renew-properties"; o.FeeAcknowledged = "N"; }, ["CHrenew-properties|", "BON|"]);
        yield return Case(SipCommand.RenewAll, "fee yes", o => o.FeeAcknowledged = "Y", ["BOY|"]);
        yield return Case(SipCommand.RenewAll, "fee omitted", o => o.FeeAcknowledged = "", [], ["BO"]);
        yield return Case(SipCommand.PatronInformation, "language", o => o.LanguageCode = "001", ["63001", "BP6|BQ10|"]);
        yield return Case(SipCommand.PatronStatus, "language", o => o.LanguageCode = "002", ["23002"]);
        yield return Case(SipCommand.FeePaid, "currency", o => o.Currency = " cad ", ["0400CAD", "BV1.25|"]);
        yield return Case(SipCommand.NoticeUpdate, "delivery date", o => { o.UseDate = true; o.DateText = "2026-11-30 14:23:45"; }, ["20261130    142345"]);
        yield return Case(SipCommand.NoticeUpdate, "unchecked date", o => { o.UseDate = false; o.DateText = "invalid"; }, ["43"]);
        yield return Case(SipCommand.Login, "algorithm identifiers", o => { o.UidAlgorithm = "A"; o.PwdAlgorithm = "B"; }, ["93AB", "VPITG|"]);
        yield return Case(SipCommand.AcsStatus, "status, width and version", o => { o.AcsStatus = "2"; o.PrintWidth = "80"; o.ProtocolVersion = "1.00"; }, ["9920801.00"]);
        foreach (var command in CommandChoice.All.Where(choice => choice.Command is not (SipCommand.Login or SipCommand.AcsStatus)))
        {
            yield return Case(command.Command, "terminal password", o => o.TerminalPassword = "terminal-secret", ["ACterminal-secret|"]);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public async Task EditedOptionsReachTheWire(OptionCase item)
    {
        string? captured = null;
        await using var server = new SipServer(async (stream, token) =>
        {
            captured = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var session = new SipSession();
        var fields = SampleFields();
        fields.Server = "127.0.0.1";
        fields.Port = server.Port.ToString(CultureInfo.InvariantCulture);
        await session.ConnectAsync(fields, "UTF-8", true);
        var options = CommandOptions.Create(item.Command);
        item.Edit(options);
        var transaction = await session.ExecuteAsync(item.Command, fields, new()
        {
            Options = options, Summary = "none", FeeType = "4", PaymentType = "0", Amount = "1.25"
        }, "UTF-8", true);
        await server.Completion;
        Assert.AreEqual(transaction.Message, captured);
        Assert.IsTrue(transaction.ChecksumValid && !transaction.ConnectionReset);
        foreach (var value in item.Contains) { StringAssert.Contains(captured!, value); }
        foreach (var value in item.Omits ?? []) { Assert.IsFalse(captured!.Contains(value, StringComparison.Ordinal), "Unexpected field: " + value); }
    }

    [TestMethod]
    [DataRow(SipCommand.Checkout, nameof(CommandOptions.DateText), "2026-02-30 10:00:00")]
    [DataRow(SipCommand.Checkin, nameof(CommandOptions.DateText), "bad")]
    [DataRow(SipCommand.Renew, nameof(CommandOptions.DateText), "")]
    [DataRow(SipCommand.NoticeUpdate, nameof(CommandOptions.DateText), "2026-01-01")]
    [DataRow(SipCommand.Login, nameof(CommandOptions.UidAlgorithm), "")]
    [DataRow(SipCommand.Login, nameof(CommandOptions.PwdAlgorithm), "|")]
    [DataRow(SipCommand.PatronInformation, nameof(CommandOptions.LanguageCode), "en")]
    [DataRow(SipCommand.PatronStatus, nameof(CommandOptions.LanguageCode), "abc")]
    [DataRow(SipCommand.FeePaid, nameof(CommandOptions.Currency), "US")]
    [DataRow(SipCommand.AcsStatus, nameof(CommandOptions.PrintWidth), "1000")]
    [DataRow(SipCommand.AcsStatus, nameof(CommandOptions.ProtocolVersion), "2.0")]
    public void InvalidOptionsIdentifyTheFieldBeforeConnecting(SipCommand command, string property, string value)
    {
        var options = CommandOptions.Create(command);
        options.UseDate = true;
        typeof(CommandOptions).GetProperty(property)!.SetValue(options, value);
        var error = Assert.Throws<CommandValidationException>(() => SipSession.Validate(command, SampleFields(),
            new() { Options = options, FeeType = "4", PaymentType = "0", Amount = "1.25" }));
        Assert.AreEqual(property, error.FieldName);
    }

    [TestMethod]
    public async Task FeeAmountsUseADecimalPointRegardlessOfUiCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            string? request = null;
            await using var server = new SipServer(async (stream, token) =>
            {
                request = await ReadMessage(stream, token);
                await WriteMessage(stream, "941", token);
            });
            using var session = new SipSession();
            var fields = SampleFields();
            fields.Server = "127.0.0.1";
            fields.Port = server.Port.ToString(CultureInfo.InvariantCulture);
            await session.ConnectAsync(fields, "UTF-8", true);
            await session.ExecuteAsync(SipCommand.FeePaid, fields, new() { FeeType = "4", PaymentType = "0", Amount = "1,25" }, "UTF-8", true);
            await server.Completion;
            StringAssert.Contains(request!, "BV1.25|");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("2", "8")]
    [DataRow("", "8")]
    public void OptionalPatronRangesAreAcceptedWithoutASummary(string start, string end)
    {
        var fields = SampleFields();
        fields.Bp = start;
        fields.Bq = end;
        SipSession.Validate(SipCommand.PatronInformation, fields, new());
    }

    [TestMethod]
    [DataRow("0", "8", "Bp")]
    [DataRow("8", "2", "Bq")]
    [DataRow("1", "bad", "Bq")]
    public void InvalidPatronRangesAreRejected(string start, string end, string invalid)
    {
        var fields = SampleFields();
        fields.Bp = start;
        fields.Bq = end;
        var error = Assert.Throws<CommandValidationException>(() => SipSession.Validate(SipCommand.PatronInformation, fields, new()));
        Assert.AreEqual(invalid, error.FieldName);
    }
}
