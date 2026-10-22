using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clc.Sip.Desktop.Models;

public partial class CommandOptions : ObservableObject
{
    [ObservableProperty] private string terminalPassword = "";
    [ObservableProperty] private string languageCode = "000";
    [ObservableProperty] private bool noBlock;
    [ObservableProperty] private bool renewalAllowed = true;
    [ObservableProperty] private bool thirdPartyAllowed = true;
    [ObservableProperty] private bool includeItemBarcode = true;
    [ObservableProperty] private bool useDate;
    [ObservableProperty] private string dateText = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);
    [ObservableProperty] private string itemProperties = "";
    [ObservableProperty] private string feeAcknowledged = "";
    [ObservableProperty] private string cancel = "";
    [ObservableProperty] private string titleIdentifier = "";
    [ObservableProperty] private string returnLocation = "";
    [ObservableProperty] private string currency = "USD";
    [ObservableProperty] private string uidAlgorithm = "0";
    [ObservableProperty] private string pwdAlgorithm = "0";
    [ObservableProperty] private string acsStatus = "0";
    [ObservableProperty] private string printWidth = "100";
    [ObservableProperty] private string protocolVersion = "2.00";

    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    public static CommandOptions Create(SipCommand command) => new()
    {
        UseDate = command == SipCommand.Renew,
        DateText = (command is SipCommand.Checkout or SipCommand.Renew ? DateTime.Now.AddDays(14) : DateTime.Now)
            .ToString(DateFormat, CultureInfo.InvariantCulture),
        FeeAcknowledged = command == SipCommand.Checkout ? "N" : ""
    };

    public CommandOptions Copy() => JsonSerializer.Deserialize<CommandOptions>(JsonSerializer.Serialize(this))!;
}

public sealed class CommandValidationException(string fieldName, string message) : ArgumentException(message)
{
    public string FieldName { get; } = fieldName;
}
