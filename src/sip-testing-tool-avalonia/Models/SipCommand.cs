namespace Clc.Sip.Desktop.Models;

public enum SipCommand
{
    Checkout, Checkin, Renew, RenewAll, ItemInformation, PatronInformation,
    PatronStatus, EndSession, FeePaid, NoticeUpdate, Login, AcsStatus
}

public sealed record CommandChoice(SipCommand Command, string Name, string Code)
{
    public static IReadOnlyList<CommandChoice> All { get; } =
    [
        new(SipCommand.Checkout, "Checkout", "11"),
        new(SipCommand.Checkin, "Checkin", "09"),
        new(SipCommand.Renew, "Renew", "29"),
        new(SipCommand.RenewAll, "Renew all", "65"),
        new(SipCommand.ItemInformation, "Item information", "17"),
        new(SipCommand.PatronInformation, "Patron information", "63"),
        new(SipCommand.PatronStatus, "Patron status", "23"),
        new(SipCommand.EndSession, "End session", "35"),
        new(SipCommand.FeePaid, "Fee paid", "37"),
        new(SipCommand.NoticeUpdate, "Notice update", "43"),
        new(SipCommand.Login, "Login", "93"),
        new(SipCommand.AcsStatus, "ACS Status", "99")
    ];

    public override string ToString() => Name;
}

public sealed record SummaryChoice(string Name, string Key)
{
    public static IReadOnlyList<SummaryChoice> All { get; } =
    [
        new("None", "none"), new("Hold items", "hold"), new("Overdue items", "overdue"),
        new("Charged items", "charged"), new("Fine items", "fine"),
        new("Recall items", "recall"), new("Unavailable holds", "unavail")
    ];
    public override string ToString() => Name;
}

public sealed record CommandFields
{
    public CommandOptions? Options { get; init; }
    public string Summary { get; init; } = "none";
    public string FeeType { get; init; } = "";
    public string PaymentType { get; init; } = "";
    public string Amount { get; init; } = "";
    public string FeeIdentifier { get; init; } = "";
    public string TransactionId { get; init; } = "";
    public NoticeStatus NoticeStatus { get; init; }
    public DateTime DeliveryDate { get; init; } = DateTime.Now;
    public NoticeMedium NoticeMedium { get; init; }
    public NotificationType NoticeType { get; init; }
    public string NoticeComment { get; init; } = "";
}
