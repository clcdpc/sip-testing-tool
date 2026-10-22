using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.Tests.TestSupport;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;
using static Clc.Sip.Desktop.Tests.TestSupport.SipServer;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
[TestCategory("Avalonia")]
public sealed class DesktopParityTests
{
    private static HeadlessUnitTestSession ui = null!;
    private const string response = "64YYYY      Y   00120261022    083815000100000000000600000000AO145|AA21870002169440|AECARD, CLC TEST|BZ0060|CA0112|CB0117|BLY|CQY|BHUSD|BV20.89|CC10.00|U1|U2NONE or UNUSED|U3NONE or UNUSED|U4YES Computer Access|U5|AV25116288 $5.00|AV25116287 $15.89 \"04 Overdue\" 1234567 A library book|AFPatron has too many fines.|AY1AZ81AC";

    [ClassInitialize]
    public static void Initialize(TestContext _) => ui = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
    [ClassCleanup]
    public static async Task Cleanup() => await ui.DisposeAsync();

    [TestMethod]
    [DataRow("ServerField")]
    [DataRow("PortField")]
    [DataRow("AoField")]
    [DataRow("ApField")]
    public Task EnterConnectsThenTabAdvancesToSelectedUsername(string name) => ui.Dispatch(async () =>
    {
        await using var server = new SipServer(async (stream, token) =>
        {
            _ = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<TextBox>(name)!.Focus();
            Press(window, Key.Enter);
            await model.ConnectCommand.ExecutionTask!;
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(model.IsConnected);
            Assert.IsTrue(window.FindControl<Button>("ConnectButton")!.IsFocused);
            Press(window, Key.Tab);
            var username = window.FindControl<TextBox>("UsernameField")!;
            Assert.IsTrue(username.IsFocused);
            Assert.AreEqual(model.Fields.Username, username.SelectedText);
            await model.LoginCommand.ExecuteAsync(null);
            await server.Completion;
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task EnterInConnectionFieldsValidatesBeforeReconnectingToTheEditedEndpoint() => ui.Dispatch(async () =>
    {
        await using var initial = new SipServer(async (stream, token) =>
        {
            var end = new byte[1];
            Assert.AreEqual(0, await stream.ReadAsync(end, token), "Reconnect should close the first connection.");
        });
        string? request = null;
        await using var replacement = new SipServer(async (stream, token) =>
        {
            request = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = initial.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var button = window.FindControl<Button>("ConnectButton")!;
            var port = window.FindControl<TextBox>("PortField")!;
            Assert.AreEqual("Connect", button.Content);
            port.Text = "";
            port.Focus();
            Press(window, Key.Enter);
            await model.ConnectCommand.ExecutionTask!;
            Assert.IsTrue(model.HasError && model.IsConnected, "An invalid edit must preserve the existing connection.");
            Assert.IsTrue(port.IsFocused);
            StringAssert.Contains(model.Status, initial.Port.ToString());
            port.Text = replacement.Port.ToString();
            Press(window, Key.Enter);
            await model.ConnectCommand.ExecutionTask!;
            await initial.Completion;
            Assert.IsTrue(model.IsConnected && !model.HasError);
            StringAssert.Contains(model.Status, replacement.Port.ToString());
            Assert.IsTrue(button.IsFocused);
            Press(window, Key.Tab);
            Assert.IsTrue(window.FindControl<TextBox>("UsernameField")!.IsFocused);
            await model.LoginCommand.ExecuteAsync(null);
            await replacement.Completion;
            StringAssert.Contains(request!, "CNtest-user|");
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("UsernameField")]
    [DataRow("PasswordField")]
    [DataRow("LocationCodeField")]
    [DataRow("UidAlgorithmField")]
    [DataRow("PwdAlgorithmField")]
    public Task EnterLogsInThenTabAdvancesToSelectedPatron(string name) => ui.Dispatch(async () =>
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
        if (name.Contains("Algorithm", StringComparison.Ordinal)) { model.SelectedCommand = Choice(SipCommand.Login); }
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Field<Control>(window, name).Focus();
            Press(window, Key.Enter);
            await model.LoginCommand.ExecutionTask!;
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(model.HasError);
            Assert.IsTrue(window.FindControl<Button>("LoginButton")!.IsFocused);
            Press(window, Key.Tab);
            var patron = window.FindControl<TextBox>("PatronField")!;
            Assert.IsTrue(patron.IsFocused);
            Assert.AreEqual(model.Fields.PatronBarcode, patron.SelectedText);
            await server.Completion;
            StringAssert.Contains(request!, "9300CNtest-user|COtest-secret|CPMAIN|VPITG|");
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task InvalidLoginOptionsRetainFocusAndDisabledLoginDoesNothing() => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            window.FindControl<TextBox>("UsernameField")!.Focus();
            Press(window, Key.Enter);
            Assert.IsFalse(model.IsBusy || model.IsConnected);
            Assert.AreEqual(0, model.LogDocument.TextLength);
            await using var server = new SipServer(async (stream, token) =>
            {
                _ = await ReadMessage(stream, token);
                await WriteMessage(stream, "941", token);
            });
            model.Fields.Server = "127.0.0.1";
            model.Fields.Port = server.Port.ToString();
            await model.ConnectCommand.ExecuteAsync(null);
            model.SelectedCommand = Choice(SipCommand.Login);
            model.Options.UidAlgorithm = "";
            model.SelectedCommand = Choice(SipCommand.Checkout);
            window.FindControl<TextBox>("UsernameField")!.Focus();
            Press(window, Key.Enter);
            await model.LoginCommand.ExecutionTask!;
            Layout(window);
            Assert.IsTrue(model.HasError);
            Assert.AreEqual(SipCommand.Login, model.SelectedCommand.Command);
            Assert.IsTrue(Field<TextBox>(window, "UidAlgorithmField").IsFocused);
            model.Options.UidAlgorithm = "0";
            await model.LoginCommand.ExecuteAsync(null);
            await server.Completion;
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(SipCommand.Checkout, "ItemPropertiesField,TerminalPasswordField,FeeAcknowledgedField,CancelField")]
    [DataRow(SipCommand.Checkin, "ReturnLocationField,ItemPropertiesField,TerminalPasswordField,CancelField")]
    [DataRow(SipCommand.Renew, "TitleIdentifierField,ItemPropertiesField,TerminalPasswordField,FeeAcknowledgedField")]
    [DataRow(SipCommand.RenewAll, "TerminalPasswordField,FeeAcknowledgedField")]
    [DataRow(SipCommand.NoticeUpdate, "NoticeCommentField,TerminalPasswordField")]
    public Task CommandFieldsFollowWindowsFormsOrderWhenTabbing(SipCommand command, string fieldNames) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(command) };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var ordered = fieldNames.Split(',').Select(name => Field<Control>(window, name)).ToArray();
            ordered[0].Focus();
            foreach (var field in ordered.Skip(1))
            {
                Press(window, Key.Tab);
                Assert.IsTrue(field.IsFocused, "Expected Tab to advance to " + field.Name);
            }
            if (command == SipCommand.Checkout)
            {
                Point Position(string name) => Field<Control>(window, name).TranslatePoint(default, window)!.Value;
                Assert.AreEqual(Position("RenewalAllowedField").Y, Position("NoBlockField").Y);
                Assert.IsTrue(Position("RenewalAllowedField").X < Position("NoBlockField").X);
                Assert.AreEqual(Position("FeeAcknowledgedField").Y, Position("CancelField").Y);
                Assert.IsTrue(Position("FeeAcknowledgedField").X < Position("CancelField").X);
                SaveFrame(window, "checkout-layout");
            }
            if (command == SipCommand.NoticeUpdate)
            {
                var previous = double.MinValue;
                foreach (var name in new[] { "NoticeStatusField", "NoticeDateField", "NoticeMediumField", "NoticeTypeField", "NoticeCommentField", "TerminalPasswordField" })
                {
                    var top = Field<Control>(window, name).TranslatePoint(default, window)!.Value.Y;
                    Assert.IsTrue(top > previous, name + " should follow the previous notice field.");
                    previous = top;
                }
                Assert.IsFalse(Field<TextBox>(window, "NoticeCommentField").AcceptsReturn);
                SaveFrame(window, "notice-layout");
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task NoticeDateValidatesInPlaceAndEditedControlsReachTheRequest() => ui.Dispatch(async () =>
    {
        string? request = null;
        await using var server = new SipServer(async (stream, token) =>
        {
            request = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.NoticeUpdate) };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var date = Field<OptionalDateTimeInput>(window, "NoticeDateField");
            var editor = Field<TextBox>(date, "DateTimeEditor");
            Field<CheckBox>(date, "IncludeDate").IsChecked = true;
            editor.Text = "invalid";
            await model.ExecuteCommand.ExecuteAsync(null);
            Assert.IsTrue(model.HasError);
            Assert.AreEqual(SipCommand.NoticeUpdate, model.SelectedCommand.Command);
            Assert.IsTrue(editor.IsFocused);
            Assert.AreEqual("invalid", editor.SelectedText);
            Assert.IsNull(request);
            editor.Text = "2026-11-30 14:23:45";
            Field<TextBox>(window, "NoticeCommentField").Text = "Delivery checked";
            Field<TextBox>(window, "TerminalPasswordField").Text = "terminal-secret";
            window.FindControl<Button>("ExecuteButton")!.Focus();
            Press(window, Key.Enter);
            await model.ExecuteCommand.ExecutionTask!;
            await server.Completion;
            Assert.IsFalse(model.HasError);
            StringAssert.Contains(request!, "20261130    142345");
            StringAssert.Contains(request!, "DEDelivery checked|");
            StringAssert.Contains(request!, "ACterminal-secret|");
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task OptionControlsPersistResetAndFollowThemes(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            foreach (var command in CommandChoice.All)
            {
                model.SelectedCommand = command;
                Layout(window);
                Assert.IsTrue(Field<Control>(window, "CommandOptionsHost").IsEffectivelyVisible);
                if (command.Command is not (SipCommand.Login or SipCommand.AcsStatus))
                {
                    Field<TextBox>(window, "TerminalPasswordField").Text = command.Name;
                    Assert.AreEqual(command.Name, model.Options.TerminalPassword);
                }
            }
            model.SelectedCommand = Choice(SipCommand.Checkout);
            Assert.AreEqual("Checkout", Field<TextBox>(window, "TerminalPasswordField").Text);
            var checkout = model.Options;
            Field<CheckBox>(window, "NoBlockField").IsChecked = true;
            Field<TextBox>(window, "ItemPropertiesField").Text = "new-properties";
            Field<ComboBox>(window, "FeeAcknowledgedField").SelectedIndex = 2;
            Assert.IsTrue(checkout.NoBlock);
            Assert.AreEqual("new-properties", checkout.ItemProperties);
            Assert.AreEqual("Y", checkout.FeeAcknowledged);
            model.ResetOptionsCommand.Execute(null);
            Assert.IsFalse(model.Options.NoBlock || model.Options.UseDate);
            Assert.AreEqual("N", model.Options.FeeAcknowledged);
            Assert.AreEqual("", model.Options.TerminalPassword);
            model.SelectedCommand = Choice(SipCommand.Checkin);
            Assert.AreEqual("Checkin", model.Options.TerminalPassword, "Reset changed another command.");
            model.SelectedCommand = Choice(SipCommand.Login);
            Assert.AreEqual("0", Field<TextBox>(window, "UidAlgorithmField").Text);
            Assert.IsTrue(model.VendorProfiles.Contains("SIP") && model.VendorProfiles.Contains("ENVISIONWARE") && model.VendorProfiles.Contains("COMPRISE"));
            foreach (var field in window.GetVisualDescendants().OfType<TextBox>()) { Assert.AreEqual('\0', field.PasswordChar); }
            Assert.AreEqual(dark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
            SaveFrame(window, dark ? "dark-options" : "light-options");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task LogFieldsExpandAndInspectWithoutChangingTheTranscript(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.AppendTransaction(new() { Message = "9300CNtest-user|COtest-password|VPITG|", Response = response });
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var raw = log.Text;
            var responseOffset = log.Document.GetLineByNumber(2).Offset;
            window.LogInspection.ExpandMessageAtOffset(responseOffset);
            Layout(window);
            var panel = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            Assert.IsTrue(panel.IsVisible);
            Assert.AreEqual(14, panel.Rows.Count(row => row.Field.IsFlag));
            Assert.AreEqual(5, panel.Rows.Count(row => row.Field.IsFlag && row.Field.State == Clc.Sip.Helpers.SipLogValueState.Set));
            Assert.IsTrue(panel.Rows.Any(row => row.Field.Name.Contains("User1", StringComparison.Ordinal)));
            Assert.IsTrue(panel.Rows.Any(row => row.Depth == 1 && row.Field.Name == "Outstanding amount" && row.Field.Value == "$5.00"));
            Assert.IsTrue(panel.Rows.Any(row => row.Depth == 1 && row.Field.Name.Contains("barcode", StringComparison.OrdinalIgnoreCase) && row.Field.Value == "1234567"));
            Assert.IsTrue(panel.GetVisualDescendants().OfType<SelectableTextBlock>().Count() < panel.Rows.Count * 3,
                "Long field tables should create controls only for visible rows.");
            StringAssert.Contains(panel.CopyText(), "Fine transaction ID");
            window.LogInspection.InspectOffset(responseOffset + 6, true);
            Assert.IsNotNull(window.LogInspection.PinnedField);
            var pinned = window.LogInspection.PinnedField;
            for (var i = 0; i < 100; i++)
            {
                window.LogInspection.InspectOffset(responseOffset + 6 + (i % 14));
            }
            Assert.AreEqual(pinned, window.LogInspection.PinnedField);
            Layout(window);
            Assert.AreEqual(raw, log.Text);
            ToolTip.SetIsOpen(log, false);
            SaveFrame(window, dark ? "dark-message-fields" : "light-message-fields");
            log.Select(0, 3);
            Assert.IsNull(window.LogInspection.PinnedField);
            Assert.AreEqual("-->", log.SelectedText);
            var cell = panel.GetVisualDescendants().OfType<SelectableTextBlock>().First();
            cell.Focus();
            Press(window, Key.Escape);
            Assert.IsFalse(panel.IsVisible);
            model.ClearLogCommand.Execute(null);
            Assert.AreEqual(0, log.Document.TextLength);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task NativeHoverClickAndMarginExpansionKeepRawTextIntact() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = response });
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var raw = log.Text;
            var view = log.TextArea.TextView;
            Point At(int offset)
            {
                var location = log.Document.GetLocation(offset);
                var position = view.GetVisualPosition(new AvaloniaEdit.TextViewPosition(location.Line, location.Column), VisualYPosition.LineMiddle);
                return view.TranslatePoint(position - view.ScrollOffset + new Vector(1, 0), window)!.Value;
            }
            var institution = raw.IndexOf("AO145", StringComparison.Ordinal);
            window.MouseMove(At(institution));
            Assert.AreEqual("AO", window.LogInspection.HoveredField?.Identifier);
            var tip = ToolTip.GetTip(log);
            window.MouseMove(At(institution + 1));
            Assert.AreSame(tip, ToolTip.GetTip(log), "Moving within an identifier should reuse its tooltip.");
            var flag = log.Document.GetLineByNumber(2).Offset + 6;
            window.MouseMove(At(flag));
            window.MouseDown(At(flag), MouseButton.Left);
            window.MouseUp(At(flag), MouseButton.Left);
            Assert.IsNotNull(window.LogInspection.PinnedField);
            Assert.AreEqual(1, window.LogInspection.PinnedField.Length);
            window.MouseMove(new Point(1200, 750));
            Assert.IsNotNull(window.LogInspection.PinnedField);

            var margin = log.TextArea.LeftMargins.First();
            var point = margin.TranslatePoint(new Point(11, 8), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Layout(window);
            Assert.IsTrue(window.FindControl<MessageFieldsPanel>("MessageFields")!.IsVisible);
            Assert.AreEqual(raw, log.Text);
            log.Focus();
            Press(window, Key.Escape);
            Assert.IsNull(window.LogInspection.PinnedField);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task TabAndShiftTabReselectTextboxValuesWhileClicksPlaceCaret(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var username = window.FindControl<TextBox>("UsernameField")!;
            var password = window.FindControl<TextBox>("PasswordField")!;
            username.Focus();
            username.SelectionStart = 2;
            username.SelectionEnd = 3;
            Press(window, Key.Tab);
            Assert.IsTrue(password.IsFocused);
            Assert.AreEqual(model.Fields.Password, password.SelectedText);
            Press(window, Key.Tab, RawInputModifiers.Shift);
            Assert.IsTrue(username.IsFocused);
            Assert.AreEqual(model.Fields.Username, username.SelectedText);
            window.KeyTextInput("replacement");
            Assert.AreEqual("replacement", model.Fields.Username);
            var point = password.TranslatePoint(new Point(22, password.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.IsTrue(password.IsFocused);
            Assert.AreEqual("", password.SelectedText);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task DateOverridesValidateAdjustAndPreserveTimeInTheCalendar() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var date = Field<OptionalDateTimeInput>(window, "DateTextField");
            var text = date.GetVisualDescendants().OfType<TextBox>().Single();
            model.Options.DateText = "2024-02-28 14:30:45";
            model.Options.UseDate = true;
            text.Focus();
            text.SelectionStart = 8;
            text.SelectionEnd = 10;
            Press(window, Key.Up);
            Assert.AreEqual("2024-02-29 14:30:45", model.Options.DateText);
            text.Text = "invalid";
            Press(window, Key.Escape);
            Assert.AreEqual("2024-02-29 14:30:45", model.Options.DateText);
            Press(window, Key.F4);
            Layout(window);
            var popup = (Flyout)typeof(OptionalDateTimeInput).GetField("popup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(date)!;
            var calendar = (Calendar)popup.Content!;
            calendar.SelectedDate = new DateTime(2024, 3, 2);
            Assert.AreEqual("2024-03-02 14:30:45", model.Options.DateText);
            Assert.IsTrue(model.Options.UseDate);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-port")]
    [DataRow("70000")]
    public Task DefaultPortCanBeSavedAndReloadedBeforeConnectValidatesIt(string port) => ui.Dispatch(async () =>
    {
        using var directory = new ScratchDirectory();
        var store = new SettingsStore(Path.Combine(directory.Path, "settings.json"));
        using var model = new MainWindowViewModel(settingsStore: store) { Fields = SampleFields() };
        var main = new MainWindow { DataContext = model };
        var settings = new SettingsWindow(model.Preferences);
        main.Show();
        var result = settings.ShowDialog<AppPreferences?>(main);
        try
        {
            Layout(settings);
            var field = settings.FindControl<TextBox>("DefaultPortField")!;
            field.Focus();
            field.SelectAll();
            Press(settings, Key.Back);
            if (port.Length > 0) { settings.KeyTextInput(port); }
            Press(settings, Key.Tab);
            Assert.AreEqual(port, ((AppPreferences)settings.DataContext!).Defaults.Port);
            settings.FindControl<Button>("SaveDefaultsButton")!.Focus();
            settings.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(result.IsCompleted, "Saving defaults should not require a connectable endpoint.");
            var saved = await result;
            Assert.IsNotNull(saved);
            Assert.AreEqual(port, saved.Defaults.Port);
            await model.SavePreferencesAsync(saved);
            Assert.AreEqual("5002", model.Fields.Port, "Saving defaults must preserve the current ribbon values.");
            Assert.AreEqual(port, (await store.LoadAsync()).Defaults.Port);

            using var exported = new MemoryStream();
            await SettingsStore.ExportAsync(exported, saved.Defaults);
            exported.Position = 0;
            Assert.AreEqual(port, (await SettingsStore.ImportAsync(exported)).Port);
            await model.ReloadDefaultsAsync();
            Assert.AreEqual(port, model.Fields.Port);
            model.Fields.Server = "127.0.0.1";
            Layout(main);
            main.FindControl<TextBox>("PortField")!.Focus();
            Press(main, Key.Enter);
            await model.ConnectCommand.ExecutionTask!;
            Assert.IsFalse(model.IsConnected || model.IsBusy);
            StringAssert.Contains(model.ErrorMessage!, "Port must be a number between 1 and 65535.");
            Assert.IsTrue(main.FindControl<TextBox>("PortField")!.IsFocused);
            Assert.AreEqual(port, model.Fields.Port, "Validation should leave the entered value intact.");
        }
        finally { settings.Close(); main.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task SavingDefaultsPreservesTheCurrentConnectionFieldsOptionsAndLog() => ui.Dispatch(async () =>
    {
        using var scratch = new ScratchDirectory();
        var store = new SettingsStore(Path.Combine(scratch.Path, "settings.json"));
        string? request = null;
        await using var server = new SipServer(async (stream, token) =>
        {
            request = await ReadMessage(stream, token);
            await WriteMessage(stream, "941", token);
        });
        using var model = new MainWindowViewModel(settingsStore: store) { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        Assert.IsTrue(model.IsConnected);
        var currentFields = model.Fields;
        var options = model.Options;
        options.ItemProperties = "keep options";
        model.AppendTransaction(new() { Message = "old request", Response = "old response" });
        var log = model.LogDocument.Text;

        await model.SavePreferencesAsync(new() { Defaults = new() { Port = "", Username = "future-user" } });
        Assert.AreSame(currentFields, model.Fields);
        Assert.AreSame(options, model.Options);
        Assert.AreEqual("keep options", model.Options.ItemProperties);
        Assert.AreEqual(log, model.LogDocument.Text);
        Assert.IsTrue(model.IsConnected);
        Assert.AreEqual("", (await store.LoadAsync()).Defaults.Port);
        await model.LoginCommand.ExecuteAsync(null);
        await server.Completion;
        StringAssert.Contains(request!, "CNtest-user|");
        Assert.IsFalse(model.HasError);

        await model.ReloadDefaultsAsync();
        Assert.AreEqual("", model.Fields.Port);
        Assert.AreEqual("future-user", model.Fields.Username);
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task SettingsThemePreviewAndTextboxSelectionRemainConsistent() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var main = new MainWindow { DataContext = model };
        var settings = new SettingsWindow(model.Preferences);
        main.Show();
        settings.Show();
        try
        {
            var editing = (AppPreferences)settings.DataContext!;
            editing.DarkTheme = true;
            Layout(settings);
            Assert.AreEqual(ThemeVariant.Dark, main.ActualThemeVariant);
            Assert.AreEqual(ThemeVariant.Dark, settings.ActualThemeVariant);
            editing.DarkTheme = false;
            Assert.AreEqual(ThemeVariant.Light, main.ActualThemeVariant);
            foreach (var text in settings.GetVisualDescendants().OfType<TextBox>()) { Assert.AreEqual('\0', text.PasswordChar); }
            editing.DarkTheme = true;
            settings.Close();
            Assert.AreEqual(ThemeVariant.Light, main.ActualThemeVariant, "Cancel should restore the original theme.");
            Assert.IsFalse(model.Preferences.DarkTheme);
        }
        finally { settings.Close(); main.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task OpenVendorProfileListAcceptsTheChoiceBeforeLogin() => ui.Dispatch(async () =>
    {
        var count = 0;
        string? request = null;
        await using var server = new SipServer(async (stream, token) =>
        {
            request = await ReadMessage(stream, token);
            Interlocked.Increment(ref count);
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
            var profiles = window.FindControl<AutoCompleteBox>("VendorProfileField")!;
            profiles.Text = "ENVISIONWARE";
            profiles.Focus();
            profiles.IsDropDownOpen = true;
            Layout(window);
            Press(window, Key.Enter);
            Assert.AreEqual(0, count, "The first Enter should accept the profile instead of logging in.");
            profiles.IsDropDownOpen = false;
            profiles.Focus();
            Press(window, Key.Enter);
            await model.LoginCommand.ExecutionTask!;
            await server.Completion;
            StringAssert.Contains(request!, "VPENVISIONWARE|");
            Assert.AreEqual(1, count);
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(window.FindControl<Button>("LoginButton")!.IsFocused);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task CheckoutControlEditsAndResetReachTheWire() => ui.Dispatch(async () =>
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
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Server = "127.0.0.1";
        model.Fields.Port = server.Port.ToString();
        await model.ConnectCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Field<CheckBox>(window, "RenewalAllowedField").IsChecked = false;
            Field<CheckBox>(window, "NoBlockField").IsChecked = true;
            var date = Field<OptionalDateTimeInput>(window, "DateTextField");
            date.IsIncluded = true;
            date.DateText = "2026-11-30 14:23:45";
            Field<TextBox>(window, "ItemPropertiesField").Text = "edited-properties";
            Field<TextBox>(window, "TerminalPasswordField").Text = "edited-secret";
            Field<ComboBox>(window, "FeeAcknowledgedField").SelectedIndex = 0;
            Field<ComboBox>(window, "CancelField").SelectedIndex = 2;
            window.FindControl<Button>("ExecuteButton")!.Focus();
            Press(window, Key.Enter);
            await model.ExecuteCommand.ExecutionTask!;
            Assert.IsTrue(requests[0].StartsWith("11NY", StringComparison.Ordinal));
            StringAssert.Contains(requests[0], "20261130    142345");
            StringAssert.Contains(requests[0], "CHedited-properties|");
            StringAssert.Contains(requests[0], "ACedited-secret|");
            StringAssert.Contains(requests[0], "BIY|");
            Assert.IsFalse(requests[0].Contains("BO", StringComparison.Ordinal));
            var reset = window.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Reset checkout options");
            reset.Focus();
            Press(window, Key.Enter);
            window.FindControl<Button>("ExecuteButton")!.Focus();
            Press(window, Key.Enter);
            await model.ExecuteCommand.ExecutionTask!;
            await server.Completion;
            Assert.AreEqual(2, requests.Count);
            Assert.IsTrue(requests[1].StartsWith("11YN", StringComparison.Ordinal));
            StringAssert.Contains(requests[1], "                  AOCLC|");
            StringAssert.Contains(requests[1], "AC|");
            StringAssert.Contains(requests[1], "BON|");
            Assert.IsFalse(requests[1].Contains("|CH", StringComparison.Ordinal) || requests[1].Contains("|BI", StringComparison.Ordinal));
            Assert.AreEqual("P123", model.Fields.PatronBarcode);
            Assert.AreEqual(2, model.LogDocument.Text.Split("--> ").Length - 1);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task ThemeShortcutPreservesFieldsAndSavesOnlyTheAppearanceChoice() => ui.Dispatch(async () =>
    {
        using var directory = new ScratchDirectory();
        var store = new SettingsStore(Path.Combine(directory.Path, "settings.json"));
        using var model = new MainWindowViewModel(settingsStore: store) { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.Options.ItemProperties = "preserve-options";
            model.AppendTransaction(new() { Message = "9300CNuser|", Response = "941" });
            var raw = model.LogDocument.Text;
            Layout(window);
            Press(window, Key.D, RawInputModifiers.Control | RawInputModifiers.Shift);
            await model.ToggleThemeCommand.ExecutionTask!;
            Assert.IsTrue((await store.LoadAsync()).DarkTheme);
            Assert.AreEqual(ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.AreEqual("P123", model.Fields.PatronBarcode);
            Assert.AreEqual("preserve-options", model.Options.ItemProperties);
            Assert.AreEqual(raw, model.LogDocument.Text);
            Press(window, Key.D, RawInputModifiers.Control | RawInputModifiers.Shift);
            await model.ToggleThemeCommand.ExecutionTask!;
            Assert.IsFalse((await store.LoadAsync()).DarkTheme);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static CommandChoice Choice(SipCommand command) => CommandChoice.All.Single(choice => choice.Command == command);
    private static T Field<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(field => field.Name == name);
    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var physical = key == Key.Enter ? PhysicalKey.Enter : key == Key.Tab ? PhysicalKey.Tab : PhysicalKey.None;
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }
    private static void Layout(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
    private static void SaveFrame(Window window, string name)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, ".git")) && !File.Exists(Path.Combine(root.FullName, ".git"))) { root = root.Parent; }
        var folder = root == null ? Path.Combine(AppContext.BaseDirectory, "screenshots") : Path.Combine(root.FullName, "artifacts", "avalonia-parity", "screenshots");
        Directory.CreateDirectory(folder);
        Layout(window);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        frame.Save(Path.Combine(folder, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
