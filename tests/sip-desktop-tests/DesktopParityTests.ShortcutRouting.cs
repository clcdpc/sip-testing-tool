using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using AvaloniaEdit;
using Clc.Sip.Desktop.Models;
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
    [DataRow(false)]
    [DataRow(true)]
    public Task ObsoleteAndEditingGesturesNeverActivateApplicationActions(bool isMacOS) => ui.Dispatch(() =>
    {
        var primary = isMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        var modifier = isMacOS ? KeyModifiers.Meta : KeyModifiers.Control;
        var provider = new ExportStorageProvider();
        using var model = new MainWindowViewModel { Fields = SampleFields(), IsConnected = true };
        model.LogDocument.Text = SearchTranscript;
        model.Options.ItemProperties = "modified";
        var window = new MainWindow(new DesktopShortcuts(isMacOS)) { DataContext = model };
        TestStorageProviderFactory.Windows.Add(window, provider);
        window.Show();
        try
        {
            Layout(window);
            var field = window.FindControl<TextBox>("UsernameField")!;
            field.Focus();
            foreach (var key in new[] { Key.C, Key.G, Key.K, Key.L, Key.P, Key.I, Key.M, Key.F, Key.S, Key.R, Key.D })
            {
                Press(window, key, primary | RawInputModifiers.Shift);
                Assert.IsTrue(field.IsFocused, "Obsolete shortcut changed focus: " + key);
                if (key != Key.G)
                {
                    Assert.IsFalse(window.KeyBindings.Any(binding => binding.Gesture == new KeyGesture(key, modifier | KeyModifiers.Shift)), key.ToString());
                }
            }
            foreach (var key in new[] { Key.C, Key.X, Key.V, Key.A, Key.Z })
            {
                Assert.IsFalse(window.KeyBindings.Any(binding => binding.Gesture == new KeyGesture(key, modifier)), "Protected editing gesture: " + key);
            }
            Assert.IsFalse(window.KeyBindings.Any(binding => binding.Gesture == new KeyGesture(Key.M, modifier)));
            Assert.AreEqual(20, window.KeyBindings.Select(binding => binding.Gesture).Distinct().Count());
            Assert.IsNull(model.ConnectCommand.ExecutionTask);
            Assert.IsNull(model.LoginCommand.ExecutionTask);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.IsNull(model.ToggleThemeCommand.ExecutionTask);
            Assert.HasCount(0, provider.Saves);
            Assert.HasCount(0, window.OwnedWindows);
            Assert.AreEqual(SearchTranscript, model.LogDocument.Text);
            Assert.IsTrue(model.HasModifiedOptions);
            Assert.IsFalse(window.FindControl<TextEditor>("MessageLog")!.SearchPanel.IsOpened);
            if (isMacOS)
            {
                Assert.IsFalse(window.KeyBindings.Any(binding => binding.Gesture == new KeyGesture(Key.Enter, KeyModifiers.Control)));
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task FinalConnectAndLoginKeysUseTheExistingCommandsOnBothPlatforms(bool isMacOS) => ui.Dispatch(async () =>
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
        var primary = isMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        var wrong = isMacOS ? RawInputModifiers.Control : RawInputModifiers.Meta;
        var window = new MainWindow(new DesktopShortcuts(isMacOS)) { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<TextBox>("PatronField")!.Focus();
            Press(window, Key.N, wrong);
            Assert.IsNull(model.ConnectCommand.ExecutionTask);
            Press(window, Key.N, primary);
            await model.ConnectCommand.ExecutionTask!;
            Assert.IsTrue(model.IsConnected);
            Press(window, Key.U, wrong);
            Assert.IsNull(model.LoginCommand.ExecutionTask);
            Press(window, Key.U, primary);
            await model.LoginCommand.ExecutionTask!;
            await server.Completion;
            StringAssert.StartsWith(request!, "93");
            StringAssert.Contains(request!, "CNtest-user|");
            Assert.AreEqual(SipCommand.ItemInformation, model.SelectedCommand.Command);
            Assert.AreEqual(1, model.LogDocument.Text.Split("--> ").Length - 1);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.IsFalse(model.HasError || model.IsBusy);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false, "shortcut")]
    [DataRow(true, "shortcut")]
    [DataRow(false, "menu")]
    [DataRow(true, "menu")]
    [DataRow(false, "context")]
    [DataRow(true, "context")]
    public Task InspectionUsesTheConfiguredPlatformAndSharesOneScopedAction(bool isMacOS, string activation) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.LogDocument.Text = "--> 9300CNuser|COpassword|\n<-- " + response;
        var window = new MainWindow(new DesktopShortcuts(isMacOS)) { DataContext = model };
        var primary = isMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var inspector = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            var menu = window.FindControl<MenuItem>("InspectMessageMenuItem")!;
            var context = log.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Name == "InspectLogMenuItem");
            Assert.AreSame(menu.Command, context.Command);
            Assert.AreEqual(menu.InputGesture, context.InputGesture);
            Assert.IsNull(context.HotKey);
            log.TextArea.Focus();
            log.CaretOffset = model.LogDocument.Text.IndexOf("AA2187", StringComparison.Ordinal);
            Press(window, Key.Enter, isMacOS ? RawInputModifiers.Control : RawInputModifiers.Meta);
            Press(window, Key.Enter);
            Assert.IsFalse(inspector.IsVisible);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Press(window, Key.F, primary);
            Layout(window);
            window.KeyTextInput("2187");
            Press(window, Key.Enter);
            Press(window, Key.Enter, RawInputModifiers.Shift);
            Press(window, Key.Enter, primary);
            Assert.IsTrue(SearchInput(log).IsFocused);
            Assert.IsFalse(inspector.IsVisible, "Find's Enter handling must stay scoped to search.");
            Press(window, Key.Escape);
            Layout(window);
            Assert.IsTrue(log.TextArea.IsFocused);
            var before = model.LogDocument.Text;
            if (activation == "menu") { ActivateAction(window, "InspectMessageMenuItem"); }
            else if (activation == "context")
            {
                log.ContextMenu.Open(log);
                Layout(window);
                Assert.IsTrue(context.Focus());
                Press(window, Key.Enter);
            }
            else { Press(window, Key.Enter, primary); }
            Layout(window);
            Assert.IsTrue(inspector.IsVisible && inspector.IsKeyboardFocusWithin, "One activation must expand and focus the inspector.");
            Assert.AreEqual(before, model.LogDocument.Text);
            Press(window, Key.Escape);
            Layout(window);
            Assert.IsFalse(inspector.IsVisible);
            window.FindControl<TextBox>("PatronField")!.Focus();
            Press(window, Key.Enter, primary);
            Assert.IsFalse(inspector.IsVisible, "Inspection must not activate from unrelated inputs.");
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.IsNull(model.LoginCommand.ExecutionTask);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task StandardEditingKeysWorkInFieldsAndSearchWithoutApplicationActions(bool isMacOS, bool search) => ui.Dispatch(async () =>
    {
        var originalIsMacOS = Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers == KeyModifiers.Meta;
        MainWindow? window = null;
        try
        {
            TestAppBuilder.ConfigurePlatformHotkeys(isMacOS);
            var primary = isMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
            using var model = new MainWindowViewModel { Fields = SampleFields() };
            model.LogDocument.Text = SearchTranscript;
            window = new MainWindow(new DesktopShortcuts(isMacOS)) { DataContext = model };
            window.Show();
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            TextBox input;
            if (search)
            {
                Press(window, Key.F, primary);
                Layout(window);
                input = SearchInput(log);
            }
            else { input = window.FindControl<TextBox>("PatronField")!; input.Focus(); }
            input.Text = "editable value";
            input.CaretIndex = 0;
            var clipboard = window.Clipboard!;
            Press(window, Key.A, primary);
            Assert.AreEqual("editable value", input.SelectedText);
            Press(window, Key.C, primary);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.AreEqual("editable value", await clipboard.TryGetTextAsync());
            Press(window, Key.X, primary);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.AreEqual("", input.Text);
            Assert.AreEqual("editable value", await clipboard.TryGetTextAsync());
            Assert.AreEqual(SearchTranscript, model.LogDocument.Text, "Cut must never clear the message log.");
            Assert.HasCount(0, window.OwnedWindows);
            Press(window, Key.V, primary);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.AreEqual("editable value", input.Text);
            Press(window, Key.A, primary);
            window.KeyTextInput("replacement");
            Assert.AreEqual("replacement", input.Text);
            Press(window, Key.Z, primary);
            Assert.AreEqual("editable value", input.Text);
            Assert.IsNull(model.ConnectCommand.ExecutionTask);
            Assert.IsNull(model.LoginCommand.ExecutionTask);
            Assert.IsNull(model.ExecuteCommand.ExecutionTask);
            Assert.IsFalse(model.HasError);
            return true;
        }
        finally
        {
            try { window?.Close(); }
            finally { TestAppBuilder.ConfigurePlatformHotkeys(originalIsMacOS); }
        }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
