using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;

namespace Clc.Sip.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new AppPreferences();
    }

    public SettingsWindow(AppPreferences preferences) : this()
        => DataContext = JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(preferences, SessionFields.JsonOptions), SessionFields.JsonOptions)!;

    private void Cancel_Click(object? sender, RoutedEventArgs args) => Close(null);

    private void Save_Click(object? sender, RoutedEventArgs args)
    {
        try
        {
            var preferences = (AppPreferences)DataContext!;
            _ = Encoding.GetEncoding(preferences.EncodingName);
            if (!string.IsNullOrWhiteSpace(preferences.UpdateFeedUrl))
            {
                _ = UpdateService.GetFeedUrl(preferences.UpdateFeedUrl);
            }

            if (!int.TryParse(preferences.Defaults.Port, out var port) || port is < 1 or > 65535)
            {
                throw new ArgumentException("Port must be a number between 1 and 65535.");
            }

            Close(preferences);
        }
        catch (Exception ex) { SettingsError.Text = ex.Message; }
    }
}
