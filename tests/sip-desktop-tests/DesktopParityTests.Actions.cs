using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.Tests.TestSupport;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;
using CommunityToolkit.Mvvm.Input;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;
using static Clc.Sip.Desktop.Tests.TestSupport.SipServer;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    private static void ActivateAction(MainWindow window, string name)
    {
        var actions = window.FindControl<MenuItem>("ActionsMenu")!;
        actions.Focus(NavigationMethod.Tab);
        Press(window, Key.Down);
        Layout(window);
        Assert.IsTrue(actions.IsSubMenuOpen, "The Actions menu must open with the keyboard.");
        Assert.IsTrue(window.FindControl<MenuItem>(name)!.Focus(NavigationMethod.Tab));
        Press(window, Key.Enter);
        Assert.IsFalse(actions.IsSubMenuOpen, "Activating an action should dismiss the menu.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ActionsMenuAndDiscoverabilityUseTheRegisteredPlatformGestures(bool isMacOS) => ui.Dispatch(() =>
    {
        var shortcuts = new DesktopShortcuts(isMacOS);
        using var model = new MainWindowViewModel();
        var window = new MainWindow(shortcuts) { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var main = window.FindControl<Menu>("MainMenu")!;
            CollectionAssert.AreEqual(new[] { "_Settings", "_Actions", "_View", "_Updates" },
                main.Items.Cast<MenuItem>().Select(item => (string)item.Header!).ToArray());
            var actions = window.FindControl<MenuItem>("ActionsMenu")!;
            CollectionAssert.AreEqual(new[]
            {
                "_Connect / Reconnect", "_Login", "_Execute selected command", "separator", "Focus SIP _command", "Focus _server address",
                "Focus _patron barcode", "Focus _item barcode", "_Focus messages", "_Next panel", "Previous panel", "separator",
                "Find in message log", "Find next", "Find previous", "Export message _log…", "Clear message log", "separator", "Reset selected options"
            }, actions.Items.Cast<Control>().Select(item => item is Separator ? "separator" : (string)((MenuItem)item).Header!).ToArray());
            CollectionAssert.AreEqual(new[] { "Always on top", "_Dark mode" },
                window.FindControl<MenuItem>("ViewMenu")!.Items.Cast<MenuItem>().Select(item => (string)item.Header!).ToArray());
            var entries = new[]
            {
                ("ExecuteMenuItem", shortcuts.Execute), ("FocusCommandMenuItem", shortcuts.FocusCommand),
                ("FocusServerMenuItem", shortcuts.FocusServer), ("FocusPatronMenuItem", shortcuts.FocusPatron),
                ("FocusItemMenuItem", shortcuts.FocusItem), ("FocusMessagesMenuItem", shortcuts.FocusMessages),
                ("ExportLogMenuItem", shortcuts.ExportLog), ("DarkModeMenuItem", shortcuts.ToggleTheme),
                ("ConnectMenuItem", shortcuts.Connect), ("LoginMenuItem", shortcuts.Login),
                ("FindLogMenuItem", shortcuts.FindLog), ("FindNextMenuItem", shortcuts.FindNext), ("FindPreviousMenuItem", shortcuts.FindPrevious),
                ("ClearLogMenuItem", shortcuts.ClearLog), ("ResetOptionsMenuItem", shortcuts.ResetOptions),
                ("NextPanelMenuItem", shortcuts.NextPanel), ("PreviousPanelMenuItem", shortcuts.PreviousPanel)
            };
            Assert.HasCount(17, window.KeyBindings);
            Assert.AreEqual(17, window.KeyBindings.Select(binding => binding.Gesture).Distinct().Count());
            foreach (var (name, gesture) in entries)
            {
                var item = window.FindControl<MenuItem>(name)!;
                Assert.AreEqual(gesture, item.InputGesture, name);
                Assert.IsNull(item.HotKey, "Only window bindings register gestures: " + name);
                Assert.AreEqual(1, window.KeyBindings.Count(binding => binding.Gesture == item.InputGesture), name);
            }
            Assert.AreSame(window.FindControl<Button>("ExecuteButton")!.Command, window.FindControl<MenuItem>("ExecuteMenuItem")!.Command);
            Assert.AreSame(window.FindControl<Button>("ExportLogButton")!.Command, window.FindControl<MenuItem>("ExportLogMenuItem")!.Command);
            Assert.AreSame(model.ToggleThemeCommand, window.FindControl<MenuItem>("DarkModeMenuItem")!.Command);
            Assert.AreEqual(MenuItemToggleType.CheckBox, window.FindControl<MenuItem>("DarkModeMenuItem")!.ToggleType);
            Assert.AreEqual(model.ExecuteLabel + " (F5)", window.FindControl<Button>("ExecuteButton")!.Content);
            Assert.AreEqual(isMacOS ? "Cmd+Shift+K" : "Ctrl+Shift+K", window.FindControl<TextBlock>("FocusCommandHint")!.Text);
            Assert.AreEqual((isMacOS ? "Cmd" : "Ctrl") + "+Shift+M to focus", window.FindControl<TextBlock>("FocusMessagesHint")!.Text);
            foreach (var (field, label, description, gesture, accessibleName) in new[]
            {
                ("ServerField", "ServerLabel", "server address", shortcuts.FocusServer, "SIP Server"),
                ("PatronField", "PatronLabel", "patron barcode", shortcuts.FocusPatron, "Patron Barcode"),
                ("ItemField", "ItemLabel", "item barcode", shortcuts.FocusItem, "Item Barcode")
            })
            {
                var input = window.FindControl<TextBox>(field)!;
                var caption = window.FindControl<TextBlock>(label)!;
                var expected = "Focus and select " + description + " (" + DesktopShortcuts.Label(gesture) + ")";
                Assert.AreEqual(expected, ToolTip.GetTip(input));
                Assert.AreEqual(expected, ToolTip.GetTip(caption));
                Assert.AreEqual(accessibleName, AutomationProperties.GetName(input));
                Assert.IsFalse(caption.Focusable);
            }
            Assert.AreEqual("Execute selected SIP command (F5)", ToolTip.GetTip(window.FindControl<Button>("ExecuteButton")!));
            Assert.AreEqual("Focus SIP command dropdown (" + DesktopShortcuts.Label(shortcuts.FocusCommand) + ")",
                ToolTip.GetTip(window.FindControl<ComboBox>("CommandPicker")!));
            Assert.AreEqual("Export message log (" + DesktopShortcuts.Label(shortcuts.ExportLog) + ")",
                ToolTip.GetTip(window.FindControl<Button>("ExportLogButton")!));
            foreach (var name in new[] { "FocusCommandHint", "FocusMessagesHint" })
            {
                var hint = window.FindControl<TextBlock>(name)!;
                Assert.IsTrue(hint.IsEffectivelyVisible && hint.Bounds.Width > 0);
                Assert.IsFalse(hint.Focusable || hint.IsTabStop);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false, "CommandPicker", "FocusCommandMenuItem", Key.K, true)]
    [DataRow(true, "CommandPicker", "FocusCommandMenuItem", Key.K, true)]
    [DataRow(false, "ServerField", "FocusServerMenuItem", Key.L, true)]
    [DataRow(true, "ServerField", "FocusServerMenuItem", Key.L, true)]
    [DataRow(false, "PatronField", "FocusPatronMenuItem", Key.P, true)]
    [DataRow(true, "PatronField", "FocusPatronMenuItem", Key.P, true)]
    [DataRow(false, "ItemField", "FocusItemMenuItem", Key.I, true)]
    [DataRow(true, "ItemField", "FocusItemMenuItem", Key.I, true)]
    public Task ActionsFocusMatchesKeyboardSelectionOnBothPlatforms(bool isMacOS, string targetName, string menuName, Key key, bool shift) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.PatronInformation) };
        var window = new MainWindow(new DesktopShortcuts(isMacOS)) { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var target = window.FindControl<Control>(targetName)!;
            var source = window.FindControl<Button>("ClearLogButton")!;
            source.Focus();
            var originalText = (target as TextBox)?.Text;
            Press(window, key, (isMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control) | (shift ? RawInputModifiers.Shift : RawInputModifiers.None));
            Assert.IsTrue(target.IsFocused);
            source.Focus();
            ActivateAction(window, menuName);
            Assert.IsTrue(target.IsFocused);
            if (target is TextBox text)
            {
                Assert.AreEqual(originalText, text.Text);
                Assert.AreEqual(originalText, text.SelectedText);
                window.KeyTextInput("scanned-replacement");
                Assert.AreEqual("scanned-replacement", text.Text);
            }
            Assert.AreEqual(SipCommand.PatronInformation, model.SelectedCommand.Command);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("CommandPicker", "FocusCommandMenuItem")]
    [DataRow("ServerField", "FocusServerMenuItem")]
    [DataRow("PatronField", "FocusPatronMenuItem")]
    [DataRow("ItemField", "FocusItemMenuItem")]
    public Task ActionsFocusAvailabilityTracksInputsAndTheirAncestors(string targetName, string menuName) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var target = window.FindControl<Control>(targetName)!;
            var menu = window.FindControl<MenuItem>(menuName)!;
            window.FindControl<MenuItem>("ActionsMenu")!.IsSubMenuOpen = true;
            Layout(window);
            var parent = (Control)target.GetVisualParent()!;
            var source = window.FindControl<Button>("ClearLogButton")!;
            foreach (var control in new[] { target, parent })
            {
                foreach (var hidden in new[] { false, true })
                {
                    control.IsEnabled = hidden;
                    control.IsVisible = !hidden;
                    Layout(window);
                    Assert.IsFalse(menu.Command!.CanExecute(null));
                    Assert.IsFalse(menu.IsEffectivelyEnabled);
                    source.Focus();
                    menu.Command.Execute(null);
                    Assert.IsTrue(source.IsFocused, "Unavailable focus actions must preserve focus.");
                    control.IsEnabled = control.IsVisible = true;
                    Layout(window);
                    Assert.IsTrue(menu.Command.CanExecute(null) && menu.IsEffectivelyEnabled);
                }
            }
            model.IsBusy = true;
            Layout(window);
            Assert.AreEqual(targetName == "CommandPicker", menu.IsEffectivelyEnabled, "Keep the existing command-picker behavior while busy.");
            model.IsBusy = false;
            Layout(window);
            Assert.IsTrue(menu.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ExecuteMenuTracksTheExistingCommandAvailability() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            var menu = window.FindControl<MenuItem>("ExecuteMenuItem")!;
            var button = window.FindControl<Button>("ExecuteButton")!;
            foreach (var (connected, busy) in new[] { (false, false), (true, false), (true, true), (true, false), (false, false) })
            {
                model.IsConnected = connected;
                model.IsBusy = busy;
                var actions = window.FindControl<MenuItem>("ActionsMenu")!;
                actions.IsSubMenuOpen = true;
                Layout(window);
                var available = model.ExecuteCommand.CanExecute(null);
                Assert.AreEqual(available, menu.IsEffectivelyEnabled);
                Assert.AreEqual(available, button.IsEffectivelyEnabled);
                Assert.AreEqual(available, menu.Command!.CanExecute(null));
                if (available) { continue; }
                Assert.IsFalse(menu.Focus(NavigationMethod.Tab));
                menu.Command.Execute(null);
                actions.IsSubMenuOpen = false;
                Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(SipCommand.Checkout)]
    [DataRow(SipCommand.Checkin)]
    [DataRow(SipCommand.PatronInformation)]
    public Task ExecuteMenuSendsExactlyOneSelectedSipRequestAndBlocksOverlap(SipCommand selected) => ui.Dispatch(async () =>
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? request = null;
        await using var server = new SipServer(async (stream, token) =>
        {
            request = await ReadMessage(stream, token);
            received.SetResult();
            await release.Task.WaitAsync(token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(selected) };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            ActivateAction(window, "ExecuteMenuItem");
            var menu = window.FindControl<MenuItem>("ExecuteMenuItem")!;
            var command = (IAsyncRelayCommand)menu.Command!;
            var operation = command.ExecutionTask!;
            await received.Task;
            var actions = window.FindControl<MenuItem>("ActionsMenu")!;
            actions.IsSubMenuOpen = true;
            Layout(window);
            Assert.IsTrue(model.IsBusy);
            Assert.IsFalse(menu.IsEffectivelyEnabled || window.FindControl<Button>("ExecuteButton")!.IsEffectivelyEnabled);
            command.Execute(null);
            Press(window, Key.F5);
            actions.IsSubMenuOpen = false;
            release.SetResult();
            await operation;
            await server.Completion;
            StringAssert.StartsWith(request!, Choice(selected).Code);
            Assert.AreEqual(1, model.LogDocument.Text.Split("--> ").Length - 1);
            Assert.IsFalse(model.HasError || model.IsBusy);
            actions.IsSubMenuOpen = true;
            Layout(window);
            Assert.IsTrue(menu.IsEffectivelyEnabled);
        }
        finally { release.TrySetResult(); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ActionsFocusMessagesUsesExistingExpanderAndEmptyLogBehavior(bool populated) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            if (populated) { model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = "941" }); }
            Layout(window);
            var before = model.LogDocument.Text;
            ActivateAction(window, "FocusMessagesMenuItem");
            Layout(window);
            if (populated) { Assert.IsTrue(VisibleMessageExpanders(window.FindControl<TextEditor>("MessageLog")!)[0].IsFocused); }
            else { Assert.IsTrue(window.FindControl<Button>("ClearLogButton")!.IsFocused); }
            Assert.AreEqual(before, model.LogDocument.Text);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ModalDialogsDisableAndGuardEveryActionsCommand() => ui.Dispatch(async () =>
    {
        var provider = new ExportStorageProvider();
        using var model = new MainWindowViewModel { Fields = SampleFields(), IsConnected = true };
        var window = new MainWindow { DataContext = model };
        TestStorageProviderFactory.Windows.Add(window, provider);
        window.Show();
        var dialog = new SettingsWindow(model.Preferences);
        try
        {
            Layout(window);
            Press(window, Key.F, ShortcutModifier | RawInputModifiers.Shift);
            Layout(window);
            window.FindControl<TextEditor>("MessageLog")!.SearchPanel.SearchPattern = "test";
            var operation = dialog.ShowDialog(window);
            Layout(dialog);
            var field = dialog.GetVisualDescendants().OfType<TextBox>().First();
            field.Focus();
            foreach (var item in window.FindControl<MenuItem>("ActionsMenu")!.Items.OfType<MenuItem>())
            {
                Assert.IsFalse(item.Command!.CanExecute(null), item.Name);
                item.Command.Execute(null);
                Assert.IsTrue(field.IsFocused);
            }
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.HasCount(0, provider.Saves);
            dialog.Close();
            await operation;
            window.Activate();
            window.FindControl<MenuItem>("ActionsMenu")!.IsSubMenuOpen = true;
            Layout(window);
            foreach (var item in window.FindControl<MenuItem>("ActionsMenu")!.Items.OfType<MenuItem>())
            {
                Assert.IsTrue(item.Command!.CanExecute(null) && item.IsEffectivelyEnabled, item.Name);
            }
        }
        finally { dialog.Close(); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task DiscoverableActionsFitMinimumAndDefaultLayouts(bool dark, bool isMacOS) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow(new DesktopShortcuts(isMacOS)) { DataContext = model };
        window.Show();
        try
        {
            var picker = window.FindControl<ComboBox>("CommandPicker")!;
            var execute = window.FindControl<Button>("ExecuteButton")!;
            var workbench = picker.GetVisualAncestors().OfType<Grid>().Single(grid => grid.ColumnDefinitions.Count == 3);
            foreach (var size in new[] { new Size(1060, 720), new Size(1280, 820) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                foreach (var panelWidth in new[] { 312, 280 })
                {
                    workbench.ColumnDefinitions[0].Width = new GridLength(panelWidth);
                    foreach (var choice in CommandChoice.All)
                    {
                        model.SelectedCommand = choice;
                        Layout(window);
                        Assert.AreEqual(model.ExecuteLabel + " (F5)", execute.Content);
                        Assert.AreEqual(32, execute.Bounds.Height, 0.5, "Hints must preserve the primary button height.");
                        var text = execute.GetVisualDescendants().OfType<TextBlock>().Single();
                        Assert.IsTrue(TextWidth(text.Text!, text.FontFamily, text.FontSize) <= execute.Bounds.Width - 22,
                            choice.Name + " clips at workbench width " + panelWidth);
                        Assert.AreEqual(panelWidth - 24, picker.Bounds.Width, 0.5, "The hint must preserve the dropdown's full width.");
                    }
                    model.SelectedCommand = Choice(SipCommand.PatronInformation);
                    Layout(window);
                    var commandHint = window.FindControl<TextBlock>("FocusCommandHint")!;
                    var hintPosition = commandHint.TranslatePoint(default, window)!.Value;
                    var pickerBottom = picker.TranslatePoint(new Point(0, picker.Bounds.Height), window)!.Value.Y;
                    Assert.IsTrue(hintPosition.Y >= pickerBottom, "The hint must sit beside the dropdown without overlapping it.");
                    var header = window.FindControl<Grid>("MessageLogHeader")!;
                    var messageHint = window.FindControl<TextBlock>("FocusMessagesHint")!;
                    foreach (var hint in new[] { commandHint, messageHint })
                    {
                        Assert.IsTrue(TextWidth(hint.Text!, hint.FontFamily, hint.FontSize) <= hint.Bounds.Width + 0.5, "Clipped hint: " + hint.Text);
                        Assert.IsFalse(hint.Focusable || hint.IsTabStop);
                        Assert.IsTrue(hint.FontSize <= 12 && hint.Opacity < 1);
                    }
                    Assert.IsTrue(header.Bounds.Height <= 50);
                    var export = window.FindControl<Button>("ExportLogButton")!;
                    var clear = window.FindControl<Button>("ClearLogButton")!;
                    Assert.IsTrue(messageHint.TranslatePoint(new Point(messageHint.Bounds.Width, 0), header)!.Value.X
                        <= clear.TranslatePoint(default, header)!.Value.X);
                    Assert.IsTrue(export.TranslatePoint(new Point(export.Bounds.Width, 0), header)!.Value.X <= header.Bounds.Width);
                    var prefix = $"actions-{(dark ? "dark" : "light")}-{(isMacOS ? "cmd" : "ctrl")}-{size.Width}-{panelWidth}";
                    model.IsConnected = false;
                    Layout(window);
                    Assert.IsFalse(execute.IsEffectivelyEnabled);
                    SaveFrame(window, prefix + "-disabled");
                    model.IsConnected = true;
                    Layout(window);
                    picker.Focus(NavigationMethod.Tab);
                    SaveFrame(window, prefix + "-picker");
                    var actions = window.FindControl<MenuItem>("ActionsMenu")!;
                    actions.Focus(NavigationMethod.Tab);
                    Press(window, Key.Down);
                    Layout(window);
                    Assert.IsTrue(actions.IsSubMenuOpen);
                    var popup = TopLevel.GetTopLevel(window.FindControl<MenuItem>("ExecuteMenuItem")!)!;
                    SaveFrame(window, prefix + "-menu-window");
                    SaveFrame(popup, prefix + "-menu");
                    foreach (var item in actions.Items.OfType<MenuItem>())
                    {
                        Assert.IsTrue(item.IsEffectivelyVisible && item.Bounds.Width > 0);
                        foreach (var text in item.GetVisualDescendants().OfType<TextBlock>().Where(text => !string.IsNullOrEmpty(text.Text)))
                        {
                            Assert.IsTrue(TextWidth(text.Text!.Replace("_", ""), text.FontFamily, text.FontSize) <= text.Bounds.Width + 1,
                                "Clipped menu label or gesture: " + text.Text);
                        }
                    }
                    Press(window, Key.Escape);
                }
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

    [TestMethod]
    [DataRow("ServerLabel", false)]
    [DataRow("ServerLabel", true)]
    [DataRow("PatronLabel", false)]
    [DataRow("PatronLabel", true)]
    [DataRow("ItemLabel", false)]
    [DataRow("ItemLabel", true)]
    public Task HoveringFieldLabelsShowsTheShortcutTooltip(string name, bool dark) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var label = window.FindControl<TextBlock>(name)!;
            ToolTip.SetShowDelay(label, 0);
            window.MouseMove(new Point(2, window.Bounds.Height - 2));
            window.MouseMove(label.TranslatePoint(new Point(TextWidth(label.Text!, label.FontFamily, label.FontSize) / 2, label.Bounds.Height / 2), window)!.Value);
            await Task.Delay(20);
            Layout(window);
            Assert.IsTrue(label.IsPointerOver, "The pointer must hover the label text.");
            Assert.IsTrue(ToolTip.GetIsOpen(label), "Hovering " + name + " must reveal its tooltip.");
            SaveFrame(window, $"actions-tooltip-{name}-{(dark ? "dark" : "light")}");
            ToolTip.SetIsOpen(label, false);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ViewMenuPreservesTheThemeCheckboxPersistenceAndAlwaysOnTop() => ui.Dispatch(async () =>
    {
        using var directory = new ScratchDirectory();
        var store = new SettingsStore(Path.Combine(directory.Path, "settings.json"));
        using var model = new MainWindowViewModel(settingsStore: store) { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var view = window.FindControl<MenuItem>("ViewMenu")!;
            var theme = window.FindControl<MenuItem>("DarkModeMenuItem")!;
            foreach (var dark in new[] { true, false })
            {
                view.Focus(NavigationMethod.Tab);
                Press(window, Key.Down);
                Layout(window);
                Assert.IsTrue(theme.Focus(NavigationMethod.Tab));
                Press(window, Key.Enter);
                await model.ToggleThemeCommand.ExecutionTask!;
                Layout(window);
                Assert.AreEqual(dark, theme.IsChecked);
                Assert.AreEqual(dark, model.Preferences.DarkTheme);
                Assert.AreEqual(dark, (await store.LoadAsync()).DarkTheme);
                Assert.AreEqual("P123", model.Fields.PatronBarcode);
                Assert.IsFalse(view.IsSubMenuOpen);
            }
            var topmost = view.Items.Cast<MenuItem>().First();
            foreach (var onTop in new[] { true, false })
            {
                view.Focus(NavigationMethod.Tab);
                Press(window, Key.Down);
                Layout(window);
                Assert.IsTrue(topmost.Focus(NavigationMethod.Tab));
                Press(window, Key.Enter);
                Layout(window);
                Assert.AreEqual(onTop, topmost.IsChecked);
                Assert.AreEqual(onTop, model.AlwaysOnTop);
                Assert.AreEqual(onTop, window.Topmost);
            }
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
