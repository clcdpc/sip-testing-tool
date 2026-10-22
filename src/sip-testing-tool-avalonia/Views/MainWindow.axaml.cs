using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.ViewModels;

namespace Clc.Sip.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? model;
    private readonly SipLogColorizer colorizer = new();
    private MainWindowViewModel Model => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        MessageLog.TextArea.TextView.LineTransformers.Add(colorizer);
        DataContextChanged += (_, _) =>
        {
            if (model != null)
            {
                model.TransactionAppended -= ScrollLog;
                model.ThemeChanged -= ApplyTheme;
            }
            model = DataContext as MainWindowViewModel;
            if (model != null)
            {
                model.TransactionAppended += ScrollLog;
                model.ThemeChanged += ApplyTheme;
            }
        };
        Closed += (_, _) =>
        {
            if (model != null)
            {
                model.TransactionAppended -= ScrollLog;
                model.ThemeChanged -= ApplyTheme;
                model.Dispose();
            }
        };
    }

    private void ScrollLog() => MessageLog.ScrollToEnd();

    private void ApplyTheme()
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = Model.Preferences.DarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        MessageLog.TextArea.TextView.Redraw();
    }

    private async void Settings_Click(object? sender, RoutedEventArgs args)
    {
        if (Model.IsBusy)
        {
            return;
        }

        var dialog = new SettingsWindow(Model.Preferences);
        var preferences = await dialog.ShowDialog<Models.AppPreferences?>(this);
        if (preferences == null)
        {
            return;
        }

        try
        {
            await Model.SavePreferencesAsync(preferences);
        }
        catch (Exception ex) { Model.ErrorMessage = "Could not save defaults: " + ex.Message; }
    }

    private async void ReloadDefaults_Click(object? sender, RoutedEventArgs args) => await Model.ReloadDefaultsAsync();

    private async void ImportSettings_Click(object? sender, RoutedEventArgs args)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import SIP settings",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("SIP settings") { Patterns = ["*.sipc"] }]
            });
            if (files.Count == 0)
            {
                return;
            }

            await using var stream = await files[0].OpenReadAsync();
            Model.Fields = await SettingsStore.ImportAsync(stream);
        }
        catch (Exception ex) { Model.ErrorMessage = "Could not import settings: " + ex.Message; }
    }

    private async void ExportSettings_Click(object? sender, RoutedEventArgs args)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export SIP settings",
                SuggestedFileName = "sip-settings.sipc",
                DefaultExtension = "sipc",
                FileTypeChoices = [new FilePickerFileType("SIP settings") { Patterns = ["*.sipc"] }]
            });
            if (file == null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await SettingsStore.ExportAsync(stream, Model.Fields.Copy());
        }
        catch (Exception ex) { Model.ErrorMessage = "Could not export settings: " + ex.Message; }
    }

    private async void ExportLog_Click(object? sender, RoutedEventArgs args)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export message log",
                SuggestedFileName = "sip-messages.txt",
                DefaultExtension = "txt",
                FileTypeChoices = [new FilePickerFileType("Text files") { Patterns = ["*.txt"] }]
            });
            if (file == null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(Model.LogDocument.Text.Replace("\n", Environment.NewLine));
        }
        catch (Exception ex) { Model.ErrorMessage = "Could not export the log: " + ex.Message; }
    }

    private void CopyLog_Click(object? sender, RoutedEventArgs args) => MessageLog.Copy();
    private void SelectLog_Click(object? sender, RoutedEventArgs args) => MessageLog.SelectAll();
}
