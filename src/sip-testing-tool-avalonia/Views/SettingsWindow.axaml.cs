using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;

namespace Clc.Sip.Desktop.Views;

public partial class SettingsWindow : Window
{
    private readonly ThemeVariant? originalTheme = Application.Current?.RequestedThemeVariant;
    private AppPreferences? editing;
    private bool accepted;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (editing != null) { editing.PropertyChanged -= PreviewTheme; }
            editing = DataContext as AppPreferences;
            if (editing != null) { editing.PropertyChanged += PreviewTheme; }
        };
        Closed += (_, _) =>
        {
            if (editing != null) { editing.PropertyChanged -= PreviewTheme; }
            if (!accepted && Application.Current != null) { Application.Current.RequestedThemeVariant = originalTheme; }
        };
        DataContext = new AppPreferences();
    }

    private void PreviewTheme(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AppPreferences.DarkTheme) && Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = editing!.DarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }
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

            accepted = true;
            Close(preferences);
        }
        catch (Exception ex) { SettingsError.Text = ex.Message; }
    }
}
