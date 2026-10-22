using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Clc.Sip.Desktop.Models;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.ViewModels;

namespace Clc.Sip.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? model;
    private readonly SipLogColorizer colorizer = new();
    public SipLogInspector LogInspection { get; }
    private MainWindowViewModel Model => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, SessionKeyDown, RoutingStrategies.Tunnel);
        LogInspection = new SipLogInspector(MessageLog, MessageFields);
        PropertyChanged += (_, args) =>
        {
            if (args.Property == ActualThemeVariantProperty) { RefreshLogTheme(); }
        };
        LogContent.RowDefinitions[0].MinHeight = 100;
        LogInspection.ExpansionChanged += expanded =>
        {
            LogContent.RowDefinitions[2].MinHeight = expanded ? 100 : 0;
            if (expanded && !MessageFields.IsVisible)
            {
                LogContent.RowDefinitions[1].Height = new GridLength(6);
                LogContent.RowDefinitions[2].Height = new GridLength(Math.Clamp(LogContent.Bounds.Height * 0.45, 150, 260));
            }
            if (!expanded)
            {
                LogContent.RowDefinitions[1].Height = new GridLength(0);
                LogContent.RowDefinitions[2].Height = new GridLength(0);
                if (MessageFields.IsKeyboardFocusWithin) { MessageLog.Focus(); }
            }
            InspectorDivider.IsVisible = MessageFields.IsVisible = expanded;
        };
        MessageLog.TextArea.TextView.LineTransformers.Add(colorizer);
        DataContextChanged += (_, _) =>
        {
            if (model != null)
            {
                model.TransactionAppended -= ScrollLog;
                model.ThemeChanged -= ApplyTheme;
                model.ValidationFailed -= FocusInvalidField;
            }
            model = DataContext as MainWindowViewModel;
            if (model != null)
            {
                model.TransactionAppended += ScrollLog;
                model.ThemeChanged += ApplyTheme;
                model.ValidationFailed += FocusInvalidField;
                ApplyTheme();
            }
        };
        Closed += (_, _) =>
        {
            LogInspection.Dispose();
            if (model != null)
            {
                model.TransactionAppended -= ScrollLog;
                model.ThemeChanged -= ApplyTheme;
                model.ValidationFailed -= FocusInvalidField;
                model.Dispose();
            }
        };
    }

    private void ScrollLog() => MessageLog.ScrollToEnd();

    private async void SessionKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || args.KeyModifiers != KeyModifiers.None || args.Source is not Control source || source is Button) { return; }
        var ancestors = source.GetVisualAncestors().Prepend(source).ToArray();
        Button? button = ancestors.Contains(ConnectionGroup) ? ConnectButton :
            ancestors.Contains(LoginGroup) || source.Name is "UidAlgorithmField" or "PwdAlgorithmField" ? LoginButton : null;
        if (button == null || (ancestors.Contains(VendorProfileField) && VendorProfileField.IsDropDownOpen)) { return; }
        args.Handled = true;
        if (button.Command is not IAsyncRelayCommand command || !command.CanExecute(null)) { return; }
        button.Focus();
        await command.ExecuteAsync(null);
        if (!Model.HasError) { button.Focus(); }
    }

    private void FocusInvalidField(string property)
    {
        var control = property == nameof(Models.CommandOptions.DateText) && Model.IsNotice ? NoticeDateField :
            this.GetVisualDescendants().OfType<Control>().FirstOrDefault(field => field.Name == property + "Field");
        if (control == null || !control.IsEffectivelyVisible)
        {
            Model.SelectedCommand = CommandChoice.All.Single(choice => choice.Command == SipCommand.Login);
            control = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(field => field.Name == property + "Field");
        }
        control?.BringIntoView();
        if (control is OptionalDateTimeInput date) { date.FocusEditor(); }
        else if (control != null)
        {
            control.Focus();
            if (control is TextBox text) { text.SelectAll(); }
        }
    }

    private void ApplyTheme()
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = Model.Preferences.DarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        RefreshLogTheme();
    }

    private void RefreshLogTheme()
    {
        MessageLog.TextArea.TextView.Redraw();
        LogInspection.RefreshTheme();
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
        catch (Exception ex) { Model.ErrorMessage = "Could not save defaults: " + ex.Message; ApplyTheme(); }
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
    private void InspectLog_Click(object? sender, RoutedEventArgs args) => LogInspection.ExpandMessageAtOffset(MessageLog.CaretOffset);
}
