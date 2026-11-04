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
    [TestMethod]
    [DataRow("shortcut")]
    [DataRow("button")]
    [DataRow("menu")]
    public Task ConnectActionsValidateAndReconnectThroughTheExistingCommand(string activation) => ui.Dispatch(async () =>
    {
        await using var initial = new SipServer(async (stream, token) =>
        {
            Assert.AreEqual(0, await stream.ReadAsync(new byte[1], token));
        });
        await using var replacement = new SipServer(async (stream, token) =>
        {
            StringAssert.StartsWith(await ReadMessage(stream, token), "93");
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = initial.Port.ToString();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var command = (IAsyncRelayCommand)window.FindControl<Button>("ConnectButton")!.Command!;
            Assert.AreSame(command, window.FindControl<MenuItem>("ConnectMenuItem")!.Command);
            Activate(window, activation, "ConnectButton", "ConnectMenuItem", Key.N);
            await command.ExecutionTask!;
            Assert.IsNotNull(model.ConnectCommand.ExecutionTask);
            Assert.IsTrue(model.IsConnected && !model.IsBusy && !model.HasError);
            StringAssert.Contains(model.Status, initial.Port.ToString());
            model.Fields.Port = "";
            Activate(window, activation, "ConnectButton", "ConnectMenuItem", Key.N);
            await command.ExecutionTask!;
            Assert.IsTrue(model.HasError && model.IsConnected);
            Assert.IsTrue(window.FindControl<TextBox>("PortField")!.IsFocused);
            StringAssert.Contains(model.Status, initial.Port.ToString());
            model.Fields.Port = replacement.Port.ToString();
            Activate(window, activation, "ConnectButton", "ConnectMenuItem", Key.N);
            await command.ExecutionTask!;
            await initial.Completion;
            Assert.IsTrue(model.IsConnected && !model.HasError);
            StringAssert.Contains(model.Status, replacement.Port.ToString());
            await model.LoginCommand.ExecuteAsync(null);
            await replacement.Completion;
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ConnectShortcutBlocksBusyAndHeldRepeatsAndRetainsCancellation() => ui.Dispatch(async () =>
    {
        var attempts = 0;
        using var session = new SipSession(async (_, token) =>
        {
            attempts++;
            await Task.Delay(Timeout.Infinite, token);
        });
        using var model = new MainWindowViewModel(session) { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            model.IsBusy = true;
            var modifiers = ShortcutModifier;
            window.KeyPress(Key.N, modifiers, PhysicalKey.N, null);
            Assert.IsNull(model.ConnectCommand.ExecutionTask);
            model.IsBusy = false;
            window.KeyPress(Key.N, modifiers, PhysicalKey.N, null);
            Assert.AreEqual(0, attempts);
            window.KeyRelease(Key.N, modifiers, PhysicalKey.N, null);
            window.KeyPress(Key.N, modifiers, PhysicalKey.N, null);
            var operation = model.ConnectCommand.ExecutionTask!;
            Assert.AreEqual(1, attempts);
            Assert.IsTrue(model.IsBusy && model.IsConnecting);
            window.KeyPress(Key.N, modifiers, PhysicalKey.N, null);
            Assert.AreSame(operation, model.ConnectCommand.ExecutionTask);
            Assert.IsFalse(window.FindControl<MenuItem>("ConnectMenuItem")!.Command!.CanExecute(null));
            model.CancelConnectCommand.Execute(null);
            await ((IAsyncRelayCommand)window.FindControl<Button>("ConnectButton")!.Command!).ExecutionTask!;
            Assert.IsFalse(model.IsConnecting || model.IsBusy || model.HasError);
            Assert.AreEqual("Connection cancelled", model.Status);
            window.KeyPress(Key.N, modifiers, PhysicalKey.N, null);
            Assert.AreEqual(1, attempts);
            window.KeyRelease(Key.N, modifiers, PhysicalKey.N, null);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ConnectShortcutKeepsTimeoutAndConnectionFailureReporting(bool timeout) => ui.Dispatch(async () =>
    {
        using var session = new SipSession((_, token) => timeout ? Task.Delay(Timeout.Infinite, token)
            : Task.FromException(new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused)),
            connectTimeout: TimeSpan.FromMilliseconds(100));
        using var model = new MainWindowViewModel(session) { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Press(window, Key.N, ShortcutModifier);
            await ((IAsyncRelayCommand)window.FindControl<Button>("ConnectButton")!.Command!).ExecutionTask!;
            Assert.IsTrue(model.HasError);
            StringAssert.Contains(model.ErrorMessage!, timeout ? "timed out" : "refused");
            Assert.AreEqual("Not connected", model.Status);
            Assert.IsFalse(model.IsConnected || model.IsConnecting || model.IsBusy);
            Assert.IsTrue(window.FindControl<MenuItem>("ConnectMenuItem")!.Command!.CanExecute(null));
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("shortcut")]
    [DataRow("button")]
    [DataRow("menu")]
    public Task LoginActionsUseLoginIndependentlyOfTheCommandPicker(string activation) => ui.Dispatch(async () =>
    {
        string? request = null;
        await using var server = new SipServer(async (stream, token) =>
        {
            request = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.ItemInformation) };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var login = (IAsyncRelayCommand)window.FindControl<Button>("LoginButton")!.Command!;
            Assert.AreSame(login, window.FindControl<MenuItem>("LoginMenuItem")!.Command);
            Press(window, Key.U, ShortcutModifier);
            Assert.IsNull(model.LoginCommand.ExecutionTask);
            await model.ConnectCommand.ExecuteAsync(null);
            model.IsBusy = true;
            Press(window, Key.U, ShortcutModifier);
            Assert.IsNull(model.LoginCommand.ExecutionTask);
            model.IsBusy = false;
            Activate(window, activation, "LoginButton", "LoginMenuItem", Key.U);
            await login.ExecutionTask!;
            await server.Completion;
            StringAssert.StartsWith(request!, "93");
            StringAssert.Contains(request!, "CNtest-user|");
            Assert.AreEqual(SipCommand.ItemInformation, model.SelectedCommand.Command);
            Assert.IsNotNull(model.LoginCommand.ExecutionTask);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.IsFalse(model.HasError || model.IsBusy);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task LoginShortcutCannotOverlapExecuteOrRepeatAfterAResponse() => ui.Dispatch(async () =>
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new SipServer(async (stream, token) =>
        {
            StringAssert.StartsWith(await ReadMessage(stream, token), "93");
            received.SetResult();
            await release.Task.WaitAsync(token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.Checkout) };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var modifiers = ShortcutModifier;
            window.KeyPress(Key.U, modifiers, PhysicalKey.U, null);
            await received.Task;
            var operation = model.LoginCommand.ExecutionTask;
            window.KeyPress(Key.U, modifiers, PhysicalKey.U, null);
            Press(window, Key.F5);
            Press(window, Key.N, modifiers);
            Assert.AreSame(operation, model.LoginCommand.ExecutionTask);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            release.SetResult();
            await ((IAsyncRelayCommand)window.FindControl<Button>("LoginButton")!.Command!).ExecutionTask!;
            window.KeyPress(Key.U, modifiers, PhysicalKey.U, null);
            Assert.AreSame(operation, model.LoginCommand.ExecutionTask);
            window.KeyRelease(Key.U, modifiers, PhysicalKey.U, null);
            await server.Completion;
            Assert.AreEqual(1, model.LogDocument.Text.Split("--> ").Length - 1);
            Assert.AreEqual(SipCommand.Checkout, model.SelectedCommand.Command);
        }
        finally { release.TrySetResult(); window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PanelNavigationWrapsWithoutSelectingOrChangingValues(bool backwards) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.PatronInformation) };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var names = new[] { "ServerField", "UsernameField", "PatronField", "ItemField", "CommandPicker", "ClearLogButton" };
            window.FindControl<TextBox>("ServerField")!.Focus();
            foreach (var text in new[] { "ServerField", "UsernameField", "PatronField", "ItemField" }.Select(name => window.FindControl<TextBox>(name)!))
            {
                text.SelectionStart = text.SelectionEnd = 2;
            }
            for (var step = 1; step <= 12; step++)
            {
                Press(window, Key.F6, backwards ? RawInputModifiers.Shift : RawInputModifiers.None);
                var index = ((backwards ? -step : step) % 6 + 6) % 6;
                var target = window.FindControl<Control>(names[index])!;
                Assert.IsTrue(target.IsFocused, $"Step {step}: expected {names[index]}, actual {window.FocusManager.GetFocusedElement()}");
                if (target is TextBox text) { Assert.AreEqual("", text.SelectedText); }
            }
            Assert.AreEqual("P123", model.Fields.PatronBarcode);
            Assert.AreEqual("I456", model.Fields.ItemBarcode);
            Assert.AreEqual(SipCommand.PatronInformation, model.SelectedCommand.Command);
            Assert.IsNull(model.ConnectCommand.ExecutionTask);
            Assert.IsNull(model.LoginCommand.ExecutionTask);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Press(window, Key.Tab);
            Assert.IsTrue(window.FindControl<TextBox>("PortField")!.IsFocused, "Tab remains within the connection group.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PanelNavigationSkipsUnavailableGroupsAndPreservesInspectorFocus(bool hide) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.AppendTransaction(new() { Message = "9300CNuser|", Response = "941" });
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var group = window.FindControl<Control>("LoginGroup")!;
            if (hide) { group.IsVisible = false; }
            else { group.IsEnabled = false; }
            window.FindControl<TextBox>("ServerField")!.Focus();
            Press(window, Key.F6);
            Assert.IsTrue(window.FindControl<TextBox>("PatronField")!.IsFocused);
            Press(window, Key.F6, RawInputModifiers.Shift);
            Assert.IsTrue(window.FindControl<TextBox>("ServerField")!.IsFocused);
            window.LogInspection.ExpandMessageAtOffset(0);
            Layout(window);
            var inspector = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            Assert.IsTrue(inspector.IsKeyboardFocusWithin);
            Press(window, Key.F6);
            Assert.IsTrue(window.FindControl<TextBox>("ServerField")!.IsFocused);
            Press(window, Key.F6, RawInputModifiers.Shift);
            Assert.IsTrue(inspector.IsKeyboardFocusWithin && inspector.IsVisible);
            ActivateAction(window, "NextPanelMenuItem");
            Assert.IsTrue(window.FindControl<TextBox>("ServerField")!.IsFocused);
            ActivateAction(window, "PreviousPanelMenuItem");
            Assert.IsTrue(inspector.IsKeyboardFocusWithin);
            Press(window, Key.Escape);
            Assert.IsFalse(inspector.IsVisible);
            model.IsBusy = true;
            Press(window, Key.F6);
            Assert.IsTrue(window.FindControl<ComboBox>("CommandPicker")!.IsFocused);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task OnlyCurrentFocusAndFindGesturesAreRegistered(bool isMacOS) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow(new DesktopShortcuts(isMacOS)) { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var source = window.FindControl<TextBox>("UsernameField")!;
            var primary = isMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
            source.Focus();
            Press(window, Key.K, primary | RawInputModifiers.Shift);
            Press(window, Key.L, primary | RawInputModifiers.Shift);
            Assert.IsTrue(source.IsFocused);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            log.TextArea.Focus();
            Press(window, Key.F, primary | RawInputModifiers.Shift);
            Press(window, Key.F, isMacOS ? RawInputModifiers.Control : RawInputModifiers.Meta);
            Press(window, Key.H, RawInputModifiers.Control);
            Assert.IsFalse(log.SearchPanel.IsOpened);
            Press(window, Key.K, primary);
            Assert.IsTrue(window.FindControl<ComboBox>("CommandPicker")!.IsFocused);
            Press(window, Key.L, primary);
            Assert.IsTrue(window.FindControl<TextBox>("ServerField")!.IsFocused);
            Press(window, Key.F, primary);
            Layout(window);
            Assert.IsTrue(SearchInput(log).IsFocused);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static void Activate(MainWindow window, string activation, string buttonName, string menuName, Key key)
    {
        if (activation == "button")
        {
            var button = buttonName == "ResetOptionsButton" ? Field<Button>(window, buttonName) : window.FindControl<Button>(buttonName)!;
            button.Focus();
            Press(window, Key.Space);
        }
        else if (activation == "menu") { ActivateAction(window, menuName); }
        else { Press(window, key, ShortcutModifier | (key == Key.X ? RawInputModifiers.Shift : RawInputModifiers.None)); }
    }
}
