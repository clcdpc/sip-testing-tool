using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
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
    private static RawInputModifiers ShortcutModifier => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PlatformShortcutsUseOneConsistentModifierAndLabel(bool isMacOS)
    {
        var shortcuts = new DesktopShortcuts(isMacOS);
        var primary = isMacOS ? KeyModifiers.Meta : KeyModifiers.Control;
        var label = isMacOS ? "Cmd+" : "Ctrl+";
        foreach (var (gesture, key, shift) in new[]
        {
            (shortcuts.FocusCommand, Key.K, false), (shortcuts.FocusPatron, Key.P, true),
            (shortcuts.FocusItem, Key.I, true), (shortcuts.FocusServer, Key.L, false),
            (shortcuts.ExportLog, Key.S, true), (shortcuts.ToggleTheme, Key.D, true),
            (shortcuts.FocusMessages, Key.M, true)
        })
        {
            Assert.AreEqual(key, gesture.Key);
            Assert.AreEqual(primary | (shift ? KeyModifiers.Shift : KeyModifiers.None), gesture.KeyModifiers);
            Assert.AreEqual(label + (shift ? "Shift+" : "") + key, DesktopShortcuts.Label(gesture));
        }
        Assert.AreEqual(Key.F5, shortcuts.Execute.Key);
        Assert.AreEqual(KeyModifiers.None, shortcuts.Execute.KeyModifiers);
        Assert.AreEqual("F5", DesktopShortcuts.Label(shortcuts.Execute));
    }

    [TestMethod]
    [DataRow(SipCommand.Checkin)]
    [DataRow(SipCommand.PatronInformation)]
    [DataRow(SipCommand.ItemInformation)]
    [DataRow(SipCommand.Login)]
    public Task F5ExecutesTheCommandSelectedInThePickerFromAnEditableField(SipCommand selected) => ui.Dispatch(async () =>
    {
        string? request = null;
        await using var server = new SipServer(async (stream, token) =>
        {
            request = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<ComboBox>("CommandPicker")!.SelectedItem = Choice(selected);
            var field = window.FindControl<TextBox>("ItemField")!;
            field.Focus();
            field.CaretIndex = 2;
            Press(window, Key.F5);
            Assert.IsNotNull(model.ExecuteCommand.ExecutionTask);
            await model.ExecuteCommand.ExecutionTask;
            Layout(window);
            await server.Completion;
            StringAssert.StartsWith(request!, Choice(selected).Code);
            Assert.AreEqual(selected, model.SelectedCommand.Command);
            Assert.AreEqual(1, model.LogDocument.Text.Split("--> ").Length - 1);
            Assert.IsFalse(model.HasError || model.IsBusy);
            Assert.IsTrue(field.IsFocused, $"Successful execution should keep focus in the field; focused={window.FocusManager.GetFocusedElement()}.");
            Assert.AreEqual(2, field.CaretIndex);
            Assert.AreEqual("", field.SelectedText, "F5 focus restoration must not select the field's contents.");
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public Task F5HonorsCommandAvailabilityEvenIfItBecomesEnabledWhileHeld(bool connected, bool busy) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), IsConnected = connected, IsBusy = busy };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<Button>("ClearLogButton")!.Focus();
            window.KeyPress(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            model.IsConnected = true;
            model.IsBusy = false;
            window.KeyPress(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask, "A repeat must not execute when command availability changes.");
            window.KeyRelease(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            Press(window, Key.F5, RawInputModifiers.Shift);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask, "Modified F5 is not the execute shortcut.");
            Assert.IsFalse(model.HasError);
            Assert.AreEqual(0, model.LogDocument.TextLength);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task F5DoesNotOverlapOrRepeatAfterAResponseUntilTheKeyIsReleased() => ui.Dispatch(async () =>
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<string>();
        await using var server = new SipServer(async (stream, token) =>
        {
            requests.Add(await ReadMessage(stream, token));
            received.SetResult();
            await release.Task.WaitAsync(token);
            await WriteMessage(stream, "941", token);
            requests.Add(await ReadMessage(stream, token));
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<TextBox>("PatronField")!.Focus();
            window.KeyPress(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            var first = model.ExecuteCommand.ExecutionTask!;
            await received.Task;
            Assert.IsTrue(model.IsBusy && !model.ExecuteCommand.CanExecute(null));
            window.KeyPress(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            Assert.AreSame(first, model.ExecuteCommand.ExecutionTask);
            release.SetResult();
            await first;
            window.KeyPress(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            Assert.AreSame(first, model.ExecuteCommand.ExecutionTask, "Even a fast response must not allow key-repeat execution.");
            Assert.AreEqual(1, model.LogDocument.Text.Split("--> ").Length - 1);
            window.KeyRelease(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            Press(window, Key.F5);
            await model.ExecuteCommand.ExecutionTask!;
            await server.Completion;
            Assert.HasCount(2, requests);
            Assert.AreEqual(2, model.LogDocument.Text.Split("--> ").Length - 1);
            Assert.IsFalse(model.HasError || model.IsBusy);
        }
        finally { release.TrySetResult(); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task F5PreservesValidationAndFocusesTheInvalidCommandField() => ui.Dispatch(async () =>
    {
        await using var server = new SipServer(async (stream, token) =>
        {
            StringAssert.StartsWith(await ReadMessage(stream, token), "43");
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.NoticeUpdate) };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        model.Options.UseDate = true;
        model.Options.DateText = "invalid-date";
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<TextBox>("PatronField")!.Focus();
            Press(window, Key.F5);
            await model.ExecuteCommand.ExecutionTask!;
            Layout(window);
            Assert.IsTrue(model.HasError && !model.IsBusy && model.IsConnected);
            Assert.AreEqual(0, model.LogDocument.TextLength, "Validation must reject the request before sending.");
            Assert.IsTrue(window.FindControl<OptionalDateTimeInput>("NoticeDateField")!.IsKeyboardFocusWithin);
            model.Options.DateText = "2026-11-03 10:15:00";
            Press(window, Key.F5);
            await model.ExecuteCommand.ExecutionTask!;
            await server.Completion;
            Assert.IsFalse(model.HasError);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task F5CompletionDoesNotStealFocusFromTheLogOrAModalDialog(bool modal) => ui.Dispatch(async () =>
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            received.SetResult();
            await release.Task.WaitAsync(token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        Window? dialog = null;
        try
        {
            Layout(window);
            window.FindControl<TextBox>("PatronField")!.Focus();
            Press(window, Key.F5);
            await received.Task;
            Control target = window.FindControl<Button>("ClearLogButton")!;
            if (modal)
            {
                var editor = new TextBox { Text = "dialog value" };
                dialog = new Window { Content = editor };
                _ = dialog.ShowDialog(window);
                Layout(dialog);
                target = editor;
            }
            target.Focus();
            release.SetResult();
            await model.ExecuteCommand.ExecutionTask!;
            await server.Completion;
            Layout(window);
            Assert.IsTrue(target.IsFocused, "Completing a SIP operation must preserve focus the user moved elsewhere.");
        }
        finally { release.TrySetResult(); dialog?.Close(); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("PatronField", Key.P, true, "P123")]
    [DataRow("ItemField", Key.I, true, "I456")]
    [DataRow("ServerField", Key.L, false, "sip.example.test")]
    public Task InputShortcutsSelectExistingTextAndAllowImmediateReplacement(string name, Key key, bool shift, string value) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var field = window.FindControl<TextBox>(name)!;
            window.FindControl<Button>("ClearLogButton")!.Focus();
            var modifiers = ShortcutModifier | (shift ? RawInputModifiers.Shift : RawInputModifiers.None);
            Press(window, key, modifiers);
            Assert.IsTrue(field.IsFocused);
            Assert.AreEqual(value, field.Text);
            Assert.AreEqual(value, field.SelectedText);
            window.KeyTextInput("replacement-123");
            Assert.AreEqual("replacement-123", field.Text);
            Assert.IsFalse(model.HasError || model.IsConnected);
            Assert.IsNull(model.ConnectCommand.ExecutionTask);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            field.CaretIndex = 3;
            field.SelectionStart = field.SelectionEnd = 3;
            window.FindControl<Button>("ClearLogButton")!.Focus();
            field.Focus();
            Assert.AreEqual("", field.SelectedText, "Unrelated programmatic focus must not select all.");
            Press(window, key, modifiers);
            Assert.AreEqual(field.Text, field.SelectedText, "The shortcut must select all even when already focused.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("PatronField", Key.P, true)]
    [DataRow("ItemField", Key.I, true)]
    [DataRow("ServerField", Key.L, false)]
    [DataRow("CommandPicker", Key.K, false)]
    public Task FocusShortcutsSkipDisabledAndHiddenInputs(string name, Key key, bool shift) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var target = window.FindControl<Control>(name)!;
            var source = window.FindControl<Button>("ClearLogButton")!;
            foreach (var hidden in new[] { false, true })
            {
                target.IsVisible = !hidden;
                target.IsEnabled = hidden;
                source.Focus();
                Press(window, key, ShortcutModifier | (shift ? RawInputModifiers.Shift : RawInputModifiers.None));
                Assert.IsTrue(source.IsFocused);
                Assert.IsFalse(target.IsFocused);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task BusyStatePreventsRibbonFocusShortcuts() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), IsBusy = true };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var source = window.FindControl<Button>("ClearLogButton")!;
            source.Focus();
            Press(window, Key.P, ShortcutModifier | RawInputModifiers.Shift);
            Press(window, Key.I, ShortcutModifier | RawInputModifiers.Shift);
            Press(window, Key.L, ShortcutModifier);
            Assert.IsTrue(source.IsFocused);
            Assert.IsNull(model.ConnectCommand.ExecutionTask);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task CommandShortcutPreservesSelectionAndNormalDropdownNavigation() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.ItemInformation) };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var picker = window.FindControl<ComboBox>("CommandPicker")!;
            window.FindControl<TextBox>("ServerField")!.Focus();
            Press(window, Key.K, ShortcutModifier);
            Assert.IsTrue(picker.IsFocused);
            Assert.AreEqual(SipCommand.ItemInformation, model.SelectedCommand.Command);
            Assert.AreSame(model.SelectedCommand, picker.SelectedItem);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Press(window, Key.Down, RawInputModifiers.Alt);
            Layout(window);
            Assert.IsTrue(picker.IsDropDownOpen);
            Press(window, Key.Down);
            Press(window, Key.Enter);
            Assert.AreEqual(SipCommand.PatronInformation, model.SelectedCommand.Command);
            Assert.IsFalse(picker.IsDropDownOpen);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task BarcodeShortcutsAndF5SupportRepeatedPatronAndItemRequests() => ui.Dispatch(async () =>
    {
        var requests = new List<string>();
        await using var server = new SipServer(async (stream, token) =>
        {
            for (var i = 0; i < 2; i++)
            {
                requests.Add(await ReadMessage(stream, token));
                await WriteMessage(stream, "941", token);
            }
        });
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.PatronStatus) };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Press(window, Key.P, ShortcutModifier | RawInputModifiers.Shift);
            window.KeyTextInput("scanned-patron");
            Press(window, Key.F5);
            await model.ExecuteCommand.ExecutionTask!;
            model.SelectedCommand = Choice(SipCommand.ItemInformation);
            Press(window, Key.I, ShortcutModifier | RawInputModifiers.Shift);
            window.KeyTextInput("scanned-item");
            Press(window, Key.F5);
            await model.ExecuteCommand.ExecutionTask!;
            await server.Completion;
            StringAssert.StartsWith(requests[0], "23");
            StringAssert.Contains(requests[0], "AAscanned-patron|");
            StringAssert.StartsWith(requests[1], "17");
            StringAssert.Contains(requests[1], "ABscanned-item|");
            Assert.AreEqual("scanned-patron", model.Fields.PatronBarcode);
            Assert.AreEqual("scanned-item", model.Fields.ItemBarcode);
            Assert.IsFalse(model.HasError || model.IsBusy);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("shortcut")]
    [DataRow("button")]
    [DataRow("menu")]
    public Task LogExportUsesOneWorkflowAndBlocksDuplicateDialogs(string activation) => ui.Dispatch(async () =>
    {
        var provider = new ExportStorageProvider();
        using var directory = new ScratchDirectory();
        var path = Path.Combine(directory.Path, "messages.txt");
        await File.WriteAllTextAsync(path, new string('x', 2000));
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.LogDocument.Text = "--> café\n<-- λ\n\n--> next";
        var window = new MainWindow { DataContext = model };
        TestStorageProviderFactory.Windows.Add(window, provider);
        window.Show();
        try
        {
            Layout(window);
            var button = window.FindControl<Button>("ExportLogButton")!;
            var menu = window.FindControl<MenuItem>("ExportLogMenuItem")!;
            Assert.AreSame(button.Command, menu.Command);
            var command = (IAsyncRelayCommand)button.Command!;
            if (activation == "button")
            {
                button.Focus();
                Press(window, Key.Space);
            }
            else if (activation == "menu")
            {
                var view = window.GetVisualDescendants().OfType<MenuItem>().Single(item => Equals(item.Header, "_View"));
                view.IsSubMenuOpen = true;
                Layout(window);
                menu.Focus(NavigationMethod.Tab);
                Press(window, Key.Enter);
            }
            else { Press(window, Key.S, ShortcutModifier | RawInputModifiers.Shift); }
            Assert.HasCount(1, provider.Saves);
            Assert.IsTrue(command.IsRunning && !command.CanExecute(null));
            var operation = command.ExecutionTask!;
            Press(window, Key.S, ShortcutModifier | RawInputModifiers.Shift);
            Press(window, Key.S, ShortcutModifier | RawInputModifiers.Shift);
            Assert.HasCount(1, provider.Saves);
            Assert.AreSame(operation, command.ExecutionTask);
            var source = window.FindControl<TextBox>("UsernameField")!;
            source.Focus();
            Press(window, Key.P, ShortcutModifier | RawInputModifiers.Shift);
            Press(window, Key.F5);
            Press(window, Key.D, ShortcutModifier | RawInputModifiers.Shift);
            Assert.IsTrue(source.IsFocused, "A native save dialog must block focus shortcuts behind it.");
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.IsNull(model.ToggleThemeCommand.ExecutionTask);
            var options = provider.Saves.Single();
            Assert.AreEqual("Export message log", options.Title);
            Assert.AreEqual("sip-messages.txt", options.SuggestedFileName);
            Assert.AreEqual("txt", options.DefaultExtension);
            Assert.AreEqual("Text files", options.FileTypeChoices!.Single().Name);
            CollectionAssert.AreEqual(new[] { "*.txt" }, options.FileTypeChoices!.Single().Patterns!.ToArray());
            provider.Result.SetResult(StorageProxy.File(path));
            await operation;
            CollectionAssert.AreEqual(new UTF8Encoding(false).GetBytes(model.LogDocument.Text.Replace("\n", Environment.NewLine)),
                await File.ReadAllBytesAsync(path), "Both paths must truncate old content and write identical UTF-8 without a BOM.");
            Assert.IsFalse(model.HasError);
            Assert.IsTrue(command.CanExecute(null));
        }
        finally { provider.Result.TrySetResult(null); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ExportCancellationAndErrorsAllowAnotherExport() => ui.Dispatch(async () =>
    {
        var provider = new ExportStorageProvider();
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        TestStorageProviderFactory.Windows.Add(window, provider);
        window.Show();
        try
        {
            Layout(window);
            var command = (IAsyncRelayCommand)window.FindControl<Button>("ExportLogButton")!.Command!;
            Press(window, Key.S, ShortcutModifier | RawInputModifiers.Shift);
            provider.Result.SetResult(null);
            await command.ExecutionTask!;
            Assert.IsFalse(model.HasError);
            provider.Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Press(window, Key.S, ShortcutModifier | RawInputModifiers.Shift);
            provider.Result.SetException(new IOException("save denied"));
            await command.ExecutionTask!;
            Assert.AreEqual("Could not export the log: save denied", model.ErrorMessage);
            provider.Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Press(window, Key.S, ShortcutModifier | RawInputModifiers.Shift);
            Assert.HasCount(3, provider.Saves);
            provider.Result.SetResult(null);
            await command.ExecutionTask!;
            Assert.IsTrue(command.CanExecute(null));
        }
        finally { provider.Result.TrySetResult(null); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ModalDialogsKeepAllMainWindowShortcutsInactive() => ui.Dispatch(async () =>
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
            var operation = dialog.ShowDialog(window);
            Layout(dialog);
            var field = dialog.GetVisualDescendants().OfType<TextBox>().First();
            field.Focus();
            foreach (var binding in window.KeyBindings)
            {
                var gesture = binding.Gesture;
                var modifiers = (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift) ? RawInputModifiers.Shift : RawInputModifiers.None)
                    | (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta) ? RawInputModifiers.Meta : RawInputModifiers.None)
                    | (gesture.KeyModifiers.HasFlag(KeyModifiers.Control) ? RawInputModifiers.Control : RawInputModifiers.None);
                Press(dialog, gesture.Key, modifiers);
                // Also guard any owner binding explicitly dispatched while the modal is active.
                binding.TryHandle(new KeyEventArgs { Key = gesture.Key, KeyModifiers = gesture.KeyModifiers });
                Assert.IsTrue(field.IsFocused);
            }
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.IsNull(model.ToggleThemeCommand.ExecutionTask);
            Assert.HasCount(0, provider.Saves);
            Assert.IsFalse(model.HasError);
            dialog.Close();
            await operation;
            window.Activate();
            Press(window, Key.P, ShortcutModifier | RawInputModifiers.Shift);
            Assert.IsTrue(window.FindControl<TextBox>("PatronField")!.IsFocused, "Shortcuts must work again after the modal closes.");
        }
        finally { dialog.Close(); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ThemeShortcutUsesOnlyThePlatformModifierAndIgnoresKeyRepeat() => ui.Dispatch(async () =>
    {
        using var directory = new ScratchDirectory();
        var store = new SettingsStore(Path.Combine(directory.Path, "settings.json"));
        using var model = new MainWindowViewModel(settingsStore: store);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<TextBox>("ServerField")!.Focus();
            var wrong = OperatingSystem.IsMacOS() ? RawInputModifiers.Control : RawInputModifiers.Meta;
            Press(window, Key.D, wrong | RawInputModifiers.Shift);
            Assert.IsNull(model.ToggleThemeCommand.ExecutionTask);
            var modifiers = ShortcutModifier | RawInputModifiers.Shift;
            window.KeyPress(Key.D, modifiers, PhysicalKey.D, null);
            await model.ToggleThemeCommand.ExecutionTask!;
            Assert.IsTrue((await store.LoadAsync()).DarkTheme);
            window.KeyPress(Key.D, modifiers, PhysicalKey.D, null);
            Assert.IsTrue(model.Preferences.DarkTheme, "One held key must not toggle the theme twice.");
            Assert.AreEqual(ThemeVariant.Dark, window.ActualThemeVariant);
            window.KeyRelease(Key.D, modifiers, PhysicalKey.D, null);
            Press(window, Key.D, modifiers);
            await model.ToggleThemeCommand.ExecutionTask!;
            Assert.IsFalse((await store.LoadAsync()).DarkTheme);
            Assert.AreEqual(ThemeVariant.Light, window.ActualThemeVariant);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ShortcutHintsMatchBindingsAndKeepTheLogHeaderCompact(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            var shortcuts = new DesktopShortcuts(OperatingSystem.IsMacOS());
            Assert.HasCount(8, window.KeyBindings);
            Assert.AreEqual(8, window.KeyBindings.Select(binding => (binding.Gesture.Key, binding.Gesture.KeyModifiers)).Distinct().Count());
            foreach (var (name, gesture) in new[]
            {
                ("DarkModeMenuItem", shortcuts.ToggleTheme), ("FocusMessagesMenuItem", shortcuts.FocusMessages),
                ("ExportLogMenuItem", shortcuts.ExportLog)
            })
            {
                var menu = window.FindControl<MenuItem>(name)!;
                Assert.AreEqual(gesture, menu.InputGesture);
                Assert.IsNull(menu.HotKey, "Menu gestures are display-only; only the window registers shortcuts.");
                Assert.IsTrue(window.KeyBindings.Any(binding => binding.Gesture == menu.InputGesture));
            }
            Assert.AreEqual("_Focus messages", window.FindControl<MenuItem>("FocusMessagesMenuItem")!.Header);
            foreach (var (name, gesture) in new[]
            {
                ("ExecuteButton", shortcuts.Execute), ("CommandPicker", shortcuts.FocusCommand), ("PatronField", shortcuts.FocusPatron),
                ("ItemField", shortcuts.FocusItem), ("ServerField", shortcuts.FocusServer), ("ExportLogButton", shortcuts.ExportLog)
            })
            {
                var tip = ToolTip.GetTip(window.FindControl<Control>(name)!) as string;
                Assert.IsNotNull(tip);
                StringAssert.Contains(tip, DesktopShortcuts.Label(gesture));
            }
            var hint = window.FindControl<TextBlock>("FocusMessagesHint")!;
            Assert.AreEqual(DesktopShortcuts.Label(shortcuts.FocusMessages) + " to focus", hint.Text);
            Assert.IsFalse(hint.Focusable || hint.IsTabStop);
            Assert.IsTrue(hint.FontSize <= 12 && hint.Opacity < 1);
            foreach (var width in new[] { 1060, 1280 })
            {
                window.Width = width;
                Layout(window);
                var header = window.FindControl<Grid>("MessageLogHeader")!;
                Assert.IsTrue(header.Bounds.Height <= 50);
                var export = window.FindControl<Button>("ExportLogButton")!;
                Assert.IsTrue(export.IsEffectivelyVisible && export.Bounds.Width > 60);
                var position = export.TranslatePoint(default, header)!.Value;
                Assert.IsTrue(position.X + export.Bounds.Width <= header.Bounds.Width);
                SaveFrame(window, $"shortcuts-{(dark ? "dark" : "light")}-{width}");
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
