using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using CommunityToolkit.Mvvm.Input;

namespace Clc.Sip.Desktop.Views;

public partial class MainWindow
{
    private bool confirmationOpen;
    private int lastPanel = -1;
    private SearchPanel? configuredSearchPanel;

    private void ConfigureAdditionalActions(DesktopShortcuts shortcuts)
    {
        var connect = new AsyncRelayCommand(() => RunSessionCommandAsync(current => current.ConnectCommand),
            () => WindowActionsAvailable && model?.ConnectCommand.CanExecute(null) == true);
        var login = new AsyncRelayCommand(() => RunSessionCommandAsync(current => current.LoginCommand),
            () => WindowActionsAvailable && model?.LoginCommand.CanExecute(null) == true);
        var clear = new AsyncRelayCommand(ClearLogAsync, () => WindowActionsAvailable && model != null);
        var reset = new AsyncRelayCommand(ResetOptionsAsync,
            () => WindowActionsAvailable && model?.ResetOptionsCommand.CanExecute(null) == true);
        var find = new RelayCommand(OpenLogSearch, () => WindowActionsAvailable && CanFocusInput(MessageLog));
        var next = new RelayCommand(() => NavigateSearch(false), CanNavigateSearch);
        var previous = new RelayCommand(() => NavigateSearch(true), CanNavigateSearch);
        var inspect = new RelayCommand(() =>
        {
            if (WindowActionsAvailable && CanFocusInput(MessageLog))
            {
                LogInspection.ExpandMessageAtOffset(MessageLog.CaretOffset);
            }
        }, () => WindowActionsAvailable && CanFocusInput(MessageLog));
        var nextPanel = new RelayCommand(() => NavigatePanel(1), CanNavigatePanels);
        var previousPanel = new RelayCommand(() => NavigatePanel(-1), CanNavigatePanels);
        RegisterAction(ConnectMenuItem, shortcuts.Connect, connect);
        RegisterAction(LoginMenuItem, shortcuts.Login, login);
        RegisterAction(ClearLogMenuItem, shortcuts.ClearLog, clear);
        RegisterAction(ResetOptionsMenuItem, shortcuts.ResetOptions, reset);
        RegisterAction(FindLogMenuItem, shortcuts.FindLog, find, suppressRepeat: false);
        // Consume navigation while search is open, including behind a native picker, so the
        // editor's scoped handler cannot bypass the window's modal guard.
        RegisterAction(FindNextMenuItem, shortcuts.FindNext, next, suppressRepeat: false,
            canHandle: () => MessageLog.SearchPanel?.IsOpened == true);
        RegisterAction(FindPreviousMenuItem, shortcuts.FindPrevious, previous, suppressRepeat: false,
            canHandle: () => MessageLog.SearchPanel?.IsOpened == true);
        AddShortcut(shortcuts.FindNextAlternative, () => next, suppressRepeat: false,
            canHandle: () => MessageLog.SearchPanel?.IsOpened == true);
        AddShortcut(shortcuts.FindPreviousAlternative, () => previous, suppressRepeat: false,
            canHandle: () => MessageLog.SearchPanel?.IsOpened == true);
        // InputGesture displays the primary gesture; the header also shows the function key.
        FindNextMenuItem.Header = "Find next (" + DesktopShortcuts.Label(shortcuts.FindNextAlternative) + ")";
        FindPreviousMenuItem.Header = "Find previous (" + DesktopShortcuts.Label(shortcuts.FindPreviousAlternative) + ")";
        ToolTip.SetTip(FindNextMenuItem, DesktopShortcuts.Label(shortcuts.FindNext) + " / " + DesktopShortcuts.Label(shortcuts.FindNextAlternative));
        ToolTip.SetTip(FindPreviousMenuItem, DesktopShortcuts.Label(shortcuts.FindPrevious) + " / " + DesktopShortcuts.Label(shortcuts.FindPreviousAlternative));
        // Only the transcript owns the inspection key. Find retains Enter/Shift+Enter.
        RegisterAction(InspectMessageMenuItem, shortcuts.InspectMessage, inspect,
            canHandle: () => MessageLog.TextArea.IsFocused);
        InspectLogMenuItem.Command = inspect;
        InspectLogMenuItem.InputGesture = shortcuts.InspectMessage;
        SetActionTip(MessageLog, "Inspect message at caret", shortcuts.InspectMessage);
        SetActionTip(InspectLogMenuItem, "Inspect message at caret", shortcuts.InspectMessage);
        RegisterAction(NextPanelMenuItem, shortcuts.NextPanel, nextPanel, suppressRepeat: false);
        RegisterAction(PreviousPanelMenuItem, shortcuts.PreviousPanel, previousPanel, suppressRepeat: false);
        ConnectButton.Command = connect;
        LoginButton.Command = login;
        ClearLogButton.Command = clear;
        FindLogButton.Command = find;
        CommandOptionsHost.ResetCommand = reset;
        SetActionTip(ConnectButton, "Connect / Reconnect", shortcuts.Connect);
        SetActionTip(LoginButton, "Execute SIP Login", shortcuts.Login);
        SetActionTip(FindLogButton, "Find in message log", shortcuts.FindLog);
        SetActionTip(ClearLogButton, "Clear message log", shortcuts.ClearLog);
        CommandOptionsHost.ResetToolTip = "Reset selected command options (" + DesktopShortcuts.Label(shortcuts.ResetOptions) + ")";
        GotFocus += (_, _) =>
        {
            var index = CurrentPanel();
            if (index >= 0) { lastPanel = index; }
        };
    }

    private void RegisterAction(MenuItem menu, KeyGesture gesture, IRelayCommand command,
        bool suppressRepeat = true, Func<bool>? canHandle = null)
    {
        menu.Command = command;
        menu.InputGesture = gesture;
        actionCommands.Add(command);
        AddShortcut(gesture, () => command, suppressRepeat, canHandle);
    }

    private static void SetActionTip(Control control, string description, KeyGesture gesture) =>
        ToolTip.SetTip(control, description + " (" + DesktopShortcuts.Label(gesture) + ")");

    private (Control Group, Control Target)[] Panels =>
    [
        (ConnectionGroup, ServerField), (LoginGroup, UsernameField), (PatronGroup, PatronField),
        (ItemGroup, ItemField), (CommandGroup, CommandPicker), (MessagesGroup, MessageLog)
    ];

    private int CurrentPanel()
    {
        if (FocusManager.GetFocusedElement() is not Visual focused) { return -1; }
        return Array.FindIndex(Panels, panel => ReferenceEquals(panel.Group, focused) || panel.Group.IsVisualAncestorOf(focused));
    }

    private bool CanNavigatePanels() => WindowActionsAvailable && Panels.Any(panel => CanFocusInput(panel.Target));

    private void NavigatePanel(int direction)
    {
        if (!WindowActionsAvailable) { return; }
        var panels = Panels;
        var index = CurrentPanel();
        if (index < 0) { index = lastPanel; }
        if (index < 0) { index = direction > 0 ? panels.Length - 1 : 0; }
        for (var count = 0; count < panels.Length; count++)
        {
            index = (index + direction + panels.Length) % panels.Length;
            var target = panels[index].Target;
            if (!CanFocusInput(target)) { continue; }
            if (target == MessageLog)
            {
                LogInspection.FocusMessages();
            }
            else
            {
                target.BringIntoView();
                // Directional navigation shows focus without the Tab-to-select-all convention.
                if (!target.Focus(NavigationMethod.Directional)) { continue; }
            }
            lastPanel = index;
            return;
        }
    }

    private void ConfigureLogSearch()
    {
        MessageLog.ApplyTemplate();
        var panel = MessageLog.SearchPanel;
        if (panel == null || ReferenceEquals(panel, configuredSearchPanel)) { return; }
        configuredSearchPanel = panel;
        // The window owns the application Find gesture. Remove the editor's Ctrl+F/H
        // aliases while retaining its search engine, buttons, and scoped navigation.
        foreach (var handler in MessageLog.TextArea.DefaultInputHandler.NestedInputHandlers.OfType<TextAreaInputHandler>())
        {
            foreach (var binding in handler.CommandBindings.Where(binding =>
                binding.Command == ApplicationCommands.Find || binding.Command == ApplicationCommands.Replace).ToArray())
            {
                handler.CommandBindings.Remove(binding);
            }
        }
        foreach (var binding in panel.CommandBindings.Where(binding =>
            binding.Command == ApplicationCommands.Find || binding.Command == ApplicationCommands.Replace).ToArray())
        {
            panel.CommandBindings.Remove(binding);
        }
        panel.SearchOptionsChanged += (_, _) => RefreshActionCommands();
        panel.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(RefreshActionCommands);
        panel.DetachedFromVisualTree += (_, _) => Dispatcher.UIThread.Post(RefreshActionCommands);
    }

    private bool CanNavigateSearch() => WindowActionsAvailable && CanFocusInput(MessageLog)
        && MessageLog.SearchPanel?.IsOpened == true && !string.IsNullOrEmpty(MessageLog.SearchPanel.SearchPattern);

    private void NavigateSearch(bool backwards)
    {
        if (!CanNavigateSearch()) { return; }
        if (backwards) { MessageLog.SearchPanel.FindPrevious(); }
        else { MessageLog.SearchPanel.FindNext(); }
    }

    private void OpenLogSearch()
    {
        if (!WindowActionsAvailable || !CanFocusInput(MessageLog)) { return; }
        ConfigureLogSearch();
        var panel = MessageLog.SearchPanel;
        panel.IsReplaceMode = false;
        panel.Open();
        panel.ApplyTemplate();
        panel.Reactivate();
        // A newly opened panel finishes its visual attachment on the dispatcher.
        Dispatcher.UIThread.Post(() =>
        {
            if (WindowActionsAvailable && panel.IsOpened) { panel.Reactivate(); }
        }, DispatcherPriority.Input);
        RefreshActionCommands();
    }

    private void LogTextChanged(object? sender, EventArgs args)
    {
        if (model?.LogDocument.TextLength != 0) { return; }
        if (MessageLog.SearchPanel?.IsOpened == true) { MessageLog.SearchPanel.Close(); }
        if (MessageLog.SearchPanel != null) { MessageLog.SearchPanel.SearchPattern = ""; }
        MessageLog.TextArea.ClearSelection();
        RefreshActionCommands();
    }

    private async Task ClearLogAsync()
    {
        var current = model;
        if (!WindowActionsAvailable || current == null) { return; }
        if (current.LogDocument.TextLength != 0
            && !await ConfirmAsync("Clear message log?", "Clear all sent and received messages? This cannot be undone.", "Clear")) { return; }
        if (!ReferenceEquals(current, model) || !WindowActionsAvailable) { return; }
        current.ClearLogCommand.Execute(null);
        ClearLogButton.Focus(NavigationMethod.Directional);
    }

    private async Task ResetOptionsAsync()
    {
        var current = model;
        if (!WindowActionsAvailable || current?.ResetOptionsCommand.CanExecute(null) != true) { return; }
        var selected = current.SelectedCommand;
        if (current.HasModifiedOptions && !await ConfirmAsync("Reset " + selected.Name + " options?",
            "Restore the default options for " + selected.Name + "? Modified options for this command will be discarded.", "Reset")) { return; }
        if (ReferenceEquals(current, model) && ReferenceEquals(selected, current.SelectedCommand)
            && WindowActionsAvailable && current.ResetOptionsCommand.CanExecute(null))
        {
            current.ResetOptionsCommand.Execute(null);
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string action)
    {
        if (!WindowActionsAvailable) { return false; }
        var focused = FocusManager.GetFocusedElement() as Control;
        confirmationOpen = true;
        RefreshActionCommands();
        try
        {
            var dialog = new ConfirmActionWindow(title, message, action);
            dialog.AddHandler(KeyUpEvent, (_, args) => shortcutKeysDown.Remove(args.Key),
                Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            return await dialog.ShowDialog<bool>(this);
        }
        finally
        {
            confirmationOpen = false;
            RefreshActionCommands();
            if (WindowActionsAvailable && focused != null && CanFocusInput(focused)) { focused.Focus(); }
        }
    }
}
