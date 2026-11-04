using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
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
    private readonly HashSet<Key> shortcutKeysDown = [];
    private readonly List<IRelayCommand> actionCommands = [];
    private readonly AsyncRelayCommand exportLogCommand;
    private bool filePickerOpen;
    public SipLogInspector LogInspection { get; }
    private MainWindowViewModel Model => (MainWindowViewModel)DataContext!;

    public MainWindow() : this(new DesktopShortcuts(OperatingSystem.IsMacOS())) { }

    internal MainWindow(DesktopShortcuts shortcuts)
    {
        InitializeComponent();
        NoticeStatusField.ItemTemplate = NoticeMediumField.ItemTemplate = NoticeTypeField.ItemTemplate =
            new FuncDataTemplate<Enum>((value, _) => new TextBlock { Text = Regex.Replace(value.ToString(), "(?<!^)([A-Z][a-z])", " $1") });
        AddHandler(KeyDownEvent, SessionKeyDown, RoutingStrategies.Tunnel);
        LogInspection = new SipLogInspector(MessageLog, MessageFields);
        LogInspection.FocusFallbackRequested += () => ClearLogButton.Focus(NavigationMethod.Tab);
        exportLogCommand = new AsyncRelayCommand(ExportLogAsync, () => WindowActionsAvailable);
        ConfigureShortcuts(shortcuts);
        Opened += (_, _) => RefreshActionCommands();
        AddHandler(KeyUpEvent, (_, args) => shortcutKeysDown.Remove(args.Key), RoutingStrategies.Tunnel, handledEventsToo: true);
        // A key released in another window will not send KeyUp to this one.
        Deactivated += (_, _) => shortcutKeysDown.Clear();
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
                model.ExecuteCommand.CanExecuteChanged -= ExecuteAvailabilityChanged;
            }
            model = DataContext as MainWindowViewModel;
            if (model != null)
            {
                model.TransactionAppended += ScrollLog;
                model.ThemeChanged += ApplyTheme;
                model.ValidationFailed += FocusInvalidField;
                model.ExecuteCommand.CanExecuteChanged += ExecuteAvailabilityChanged;
                ApplyTheme();
            }
            RefreshActionCommands();
        };
        Closed += (_, _) =>
        {
            LogInspection.Dispose();
            if (model != null)
            {
                model.TransactionAppended -= ScrollLog;
                model.ThemeChanged -= ApplyTheme;
                model.ValidationFailed -= FocusInvalidField;
                model.ExecuteCommand.CanExecuteChanged -= ExecuteAvailabilityChanged;
                model.Dispose();
            }
        };
    }

    private void ScrollLog() => MessageLog.ScrollToEnd();

    private bool WindowActionsAvailable => IsEffectivelyEnabled && IsEffectivelyVisible && !filePickerOpen
        && !OwnedWindows.Any(window => window.IsVisible && window.IsDialog);

    private void RefreshActionCommands()
    {
        foreach (var command in actionCommands) { command.NotifyCanExecuteChanged(); }
    }

    private void ExecuteAvailabilityChanged(object? sender, EventArgs args) => RefreshActionCommands();

    private void SetFilePickerOpen(bool open)
    {
        filePickerOpen = open;
        RefreshActionCommands();
    }

    private void ConfigureShortcuts(DesktopShortcuts shortcuts)
    {
        var execute = new AsyncRelayCommand(ExecuteSelectedAsync,
            () => WindowActionsAvailable && model?.ExecuteCommand.CanExecute(null) == true);
        actionCommands.Add(execute);
        actionCommands.Add(exportLogCommand);
        ExecuteButton.Command = ExecuteMenuItem.Command = execute;
        ExecuteMenuItem.InputGesture = shortcuts.Execute;
        AddShortcut(shortcuts.Execute, () => execute);
        AddFocusShortcut(shortcuts.FocusCommand, CommandPicker, FocusCommandMenuItem);
        AddFocusShortcut(shortcuts.FocusPatron, PatronField, FocusPatronMenuItem);
        AddFocusShortcut(shortcuts.FocusItem, ItemField, FocusItemMenuItem);
        AddFocusShortcut(shortcuts.FocusServer, ServerField, FocusServerMenuItem);
        AddShortcut(shortcuts.ToggleTheme, () => model?.ToggleThemeCommand);
        AddShortcut(shortcuts.ExportLog, () => exportLogCommand);

        var focusMessages = new RelayCommand(() =>
        {
            if (WindowActionsAvailable && CanFocusInput(MessageLog)) { LogInspection.FocusMessages(); }
        }, () => WindowActionsAvailable && CanFocusInput(MessageLog));
        actionCommands.Add(focusMessages);
        FocusMessagesMenuItem.Command = focusMessages;
        AddShortcut(shortcuts.FocusMessages, () => FocusMessagesMenuItem.Command);
        FocusMessagesMenuItem.InputGesture = shortcuts.FocusMessages;
        DarkModeMenuItem.InputGesture = shortcuts.ToggleTheme;
        ExportLogButton.Command = ExportLogMenuItem.Command = exportLogCommand;
        ExportLogMenuItem.InputGesture = shortcuts.ExportLog;
        FocusMessagesHint.Text = DesktopShortcuts.Label(shortcuts.FocusMessages) + " to focus";
        FocusCommandHint.Text = DesktopShortcuts.Label(shortcuts.FocusCommand);
        ExecuteButton.Bind(ContentControl.ContentProperty, new Binding(nameof(MainWindowViewModel.ExecuteLabel))
        {
            StringFormat = "{0} (" + DesktopShortcuts.Label(shortcuts.Execute) + ")"
        });

        ToolTip.SetTip(ExecuteButton, "Execute selected SIP command (" + DesktopShortcuts.Label(shortcuts.Execute) + ")");
        ToolTip.SetTip(CommandPicker, "Focus SIP command dropdown (" + DesktopShortcuts.Label(shortcuts.FocusCommand) + ")");
        SetFieldTip(PatronField, PatronLabel, "patron barcode", shortcuts.FocusPatron);
        SetFieldTip(ItemField, ItemLabel, "item barcode", shortcuts.FocusItem);
        SetFieldTip(ServerField, ServerLabel, "server address", shortcuts.FocusServer);
        ToolTip.SetTip(ExportLogButton, "Export message log (" + DesktopShortcuts.Label(shortcuts.ExportLog) + ")");
        // Effective enabled state propagates to each input. Visibility also depends on
        // its ancestors, so refresh availability when any of those are shown or hidden.
        foreach (var visual in new Control[] { CommandPicker, ServerField, PatronField, ItemField, MessageLog }
            .SelectMany(control => control.GetVisualAncestors().Prepend(control)).Distinct())
        {
            visual.PropertyChanged += (_, args) =>
            {
                if (args.Property == IsEffectivelyEnabledProperty || args.Property == IsVisibleProperty)
                {
                    RefreshActionCommands();
                    // Visibility and modal ownership finish propagating after the property event.
                    Avalonia.Threading.Dispatcher.UIThread.Post(RefreshActionCommands);
                }
            };
        }
    }

    private static void SetFieldTip(Control field, Control label, string name, KeyGesture gesture)
    {
        var tip = "Focus and select " + name + " (" + DesktopShortcuts.Label(gesture) + ")";
        ToolTip.SetTip(field, tip);
        ToolTip.SetTip(label, tip);
    }

    private void AddFocusShortcut(KeyGesture gesture, Control control, MenuItem menu)
    {
        var command = new RelayCommand(() => FocusInput(control), () => WindowActionsAvailable && CanFocusInput(control));
        actionCommands.Add(command);
        menu.Command = command;
        menu.InputGesture = gesture;
        AddShortcut(gesture, () => command);
    }

    private void AddShortcut(KeyGesture gesture, Func<ICommand?> command)
    {
        KeyBindings.Add(new KeyBinding
        {
            Gesture = gesture,
            Command = new RelayCommand(() =>
            {
                // KeyBinding handles KeyDown before routed handlers. Latch here, even if the
                // underlying command is disabled, so a held key cannot run it when it becomes enabled.
                if (!WindowActionsAvailable || !shortcutKeysDown.Add(gesture.Key)) { return; }
                var action = command();
                if (action?.CanExecute(null) == true) { action.Execute(null); }
            })
        });
    }

    private async Task ExecuteSelectedAsync()
    {
        var current = model;
        if (!WindowActionsAvailable || current?.ExecuteCommand.CanExecute(null) != true) { return; }
        var focused = FocusManager.GetFocusedElement() as Control;
        await current.ExecuteCommand.ExecuteAsync(null);
        // Busy state disables inputs and clears their focus. Restore it only if neither
        // validation nor the user has focused something else while the request was running.
        if (ReferenceEquals(current, model) && !current.HasError && WindowActionsAvailable && IsActive
            && FocusManager.GetFocusedElement() == null && focused?.IsEffectivelyEnabled == true && focused.IsEffectivelyVisible)
        {
            focused.Focus();
        }
    }

    private static bool CanFocusInput(Control control) => control.IsEffectivelyEnabled && control.IsEffectivelyVisible;

    private void FocusInput(Control control)
    {
        if (!WindowActionsAvailable || !CanFocusInput(control) || !control.Focus()) { return; }
        if (control is TextBox text) { text.SelectAll(); }
    }

    private void VendorProfiles_Click(object? sender, RoutedEventArgs args)
    {
        var open = !VendorProfileField.IsDropDownOpen;
        VendorProfileField.Focus();
        VendorProfileField.IsDropDownOpen = open;
    }

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
        if (!WindowActionsAvailable) { return; }
        SetFilePickerOpen(true);
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
        finally { SetFilePickerOpen(false); }
    }

    private async void ExportSettings_Click(object? sender, RoutedEventArgs args)
    {
        if (!WindowActionsAvailable) { return; }
        SetFilePickerOpen(true);
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
        finally { SetFilePickerOpen(false); }
    }

    private async Task ExportLogAsync()
    {
        if (!WindowActionsAvailable) { return; }
        SetFilePickerOpen(true);
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
        finally { SetFilePickerOpen(false); }
    }

    private void CopyLog_Click(object? sender, RoutedEventArgs args) => MessageLog.Copy();
    private void SelectLog_Click(object? sender, RoutedEventArgs args) => MessageLog.SelectAll();
    private void InspectLog_Click(object? sender, RoutedEventArgs args) => LogInspection.ExpandMessageAtOffset(MessageLog.CaretOffset);
}
