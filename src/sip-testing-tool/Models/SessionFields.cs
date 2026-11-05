using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clc.Sip.Desktop.Models;

public partial class SessionFields : ObservableObject
{
    [ObservableProperty] private string server = "";
    [ObservableProperty] private string port = "";
    [ObservableProperty] private string ao = "";
    [ObservableProperty] private string ap = "";
    [ObservableProperty] private string username = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string locationCode = "";
    [ObservableProperty] private string vendorProfile = "";
    [ObservableProperty] private string patronBarcode = "";
    [ObservableProperty]
    [property: System.Text.Json.Serialization.JsonPropertyName("patronPIN")]
    private string patronPin = "";
    [ObservableProperty] private string itemBarcode = "";
    [ObservableProperty] private string bp = "1";
    [ObservableProperty] private string bq = "5";

    public SessionFields Copy() => JsonSerializer.Deserialize<SessionFields>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
