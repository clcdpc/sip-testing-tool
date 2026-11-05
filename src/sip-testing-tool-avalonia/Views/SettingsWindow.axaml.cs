using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly FieldValidationAccessibility fieldValidation;

    public SettingsWindow()
    {
        InitializeComponent();
        fieldValidation = new FieldValidationAccessibility(() => SettingsError.Text = "");
        ControlAutomationPeer.CreatePeerForElement(SettingsError);
        AccessibilityAnnouncement.IsVisible = OperatingSystem.IsWindows();
        ControlAutomationPeer.CreatePeerForElement(AccessibilityAnnouncement);
        EncodingField.ItemsSource = new[] { "UTF-8", "ISO-8859-1" };
        DataContextChanged += (_, _) =>
        {
            fieldValidation.Clear();
            if (editing != null) { editing.PropertyChanged -= PreviewTheme; }
            editing = DataContext as AppPreferences;
            if (editing != null) { editing.PropertyChanged += PreviewTheme; }
        };
        Closed += (_, _) =>
        {
            fieldValidation.Clear();
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

    private void ChooseEncoding_Click(object? sender, RoutedEventArgs args)
    {
        var open = !EncodingField.IsDropDownOpen;
        EncodingField.Focus();
        EncodingField.IsDropDownOpen = open;
    }

    private void Save_Click(object? sender, RoutedEventArgs args)
    {
        fieldValidation.Clear();
        SettingsError.Text = "";
        Control field = EncodingField;
        try
        {
            var preferences = (AppPreferences)DataContext!;
            _ = Encoding.GetEncoding(preferences.EncodingName);
            field = UpdateRepositoryField;
            if (!string.IsNullOrWhiteSpace(preferences.UpdateFeedUrl))
            {
                _ = UpdateService.GetRepositoryUrl(preferences.UpdateFeedUrl);
            }

            accepted = true;
            Close(preferences);
        }
        catch (Exception ex)
        {
            SettingsError.Text = ex.Message;
            fieldValidation.Show(field, ex.Message);
            field.BringIntoView();
            field.Focus(NavigationMethod.Directional);
            if (field is TextBox text) { text.SelectAll(); }
        }
    }
}
