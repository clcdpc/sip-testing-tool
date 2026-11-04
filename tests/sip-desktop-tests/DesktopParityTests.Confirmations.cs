using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Tests.TestSupport;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;
using CommunityToolkit.Mvvm.Input;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    private static void RespondToConfirmation(MainWindow window, bool confirm)
    {
        var dialog = window.OwnedWindows.OfType<ConfirmActionWindow>().Single();
        Layout(dialog);
        Assert.IsTrue(Field<Button>(dialog, "CancelButton").IsFocused, "Destructive dialogs must start on Cancel.");
        Assert.IsTrue(Field<Button>(dialog, "CancelButton").IsDefault);
        Assert.IsFalse(Field<Button>(dialog, "ConfirmButton").IsDefault);
        if (confirm) { Field<Button>(dialog, "ConfirmButton").Focus(); }
        dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
    }

    [TestMethod]
    [DataRow("shortcut", false)]
    [DataRow("shortcut", true)]
    [DataRow("button", false)]
    [DataRow("button", true)]
    [DataRow("menu", false)]
    [DataRow("menu", true)]
    public Task ClearActionsShareConfirmationAndReconcileSearchAndInspection(string activation, bool confirm) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), IsConnected = true };
        model.AppendTransaction(new() { Message = "9300CNuser|COsecret|", Response = "941" });
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            window.LogInspection.ExpandMessageAtOffset(0);
            Press(window, Key.F, ShortcutModifier);
            Layout(window);
            SearchInput(log).Text = "user";
            var original = model.LogDocument.Text;
            var command = (IAsyncRelayCommand)window.FindControl<Button>("ClearLogButton")!.Command!;
            Assert.AreSame(command, window.FindControl<MenuItem>("ClearLogMenuItem")!.Command);
            Activate(window, activation, "ClearLogButton", "ClearLogMenuItem", Key.X);
            var operation = command.ExecutionTask!;
            Assert.HasCount(1, window.OwnedWindows);
            var dialog = window.OwnedWindows.Single();
            StringAssert.Contains(dialog.Title!, "Clear message log");
            Press(window, Key.X, ShortcutModifier | RawInputModifiers.Shift);
            command.Execute(null);
            Assert.HasCount(1, window.OwnedWindows, "Repeated activation must not create another dialog.");
            Assert.AreEqual(original, model.LogDocument.Text);
            RespondToConfirmation(window, confirm);
            await operation;
            Layout(window);
            Assert.HasCount(0, window.OwnedWindows);
            Assert.IsTrue(model.IsConnected);
            if (confirm)
            {
                Assert.AreEqual("", model.LogDocument.Text);
                Assert.IsFalse(log.SearchPanel.IsOpened);
                Assert.AreEqual("", log.SearchPanel.SearchPattern);
                Assert.AreEqual(0, log.SelectionLength);
                Assert.IsNull(window.LogInspection.PinnedField);
                Assert.IsFalse(window.FindControl<MessageFieldsPanel>("MessageFields")!.IsVisible);
                Assert.IsTrue(window.FindControl<Button>("ClearLogButton")!.IsFocused);
                model.AppendTransaction(new() { Message = "99", Response = "98" });
                Layout(window);
                Assert.IsNotEmpty(MessageExpanders(log));
                StringAssert.Contains(log.Text, "--> 99");
            }
            else
            {
                Assert.AreEqual(original, model.LogDocument.Text);
                Assert.IsTrue(log.SearchPanel.IsOpened);
                Assert.AreEqual("user", log.SearchPanel.SearchPattern);
                Assert.IsTrue(window.FindControl<MessageFieldsPanel>("MessageFields")!.IsVisible);
            }
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) { dialog.Close(); } window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("shortcut")]
    [DataRow("button")]
    [DataRow("menu")]
    public Task ClearingAnEmptyLogNeedsNoConfirmation(string activation) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Activate(window, activation, "ClearLogButton", "ClearLogMenuItem", Key.X);
            await ((IAsyncRelayCommand)window.FindControl<Button>("ClearLogButton")!.Command!).ExecutionTask!;
            Assert.HasCount(0, window.OwnedWindows);
            Assert.AreEqual(0, model.LogDocument.TextLength);
            Assert.IsFalse(model.HasError);
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow("shortcut", false)]
    [DataRow("shortcut", true)]
    [DataRow("button", false)]
    [DataRow("button", true)]
    [DataRow("menu", false)]
    [DataRow("menu", true)]
    public Task ResetActionsShareConfirmationAndPreserveUnrelatedState(string activation, bool confirm) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.Checkin) };
        model.Options.ReturnLocation = "keep-other-command";
        model.SelectedCommand = Choice(SipCommand.Checkout);
        model.Options.ItemProperties = "modified";
        model.Options.NoBlock = true;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var reset = Field<Button>(window, "ResetOptionsButton");
            var command = (IAsyncRelayCommand)reset.Command!;
            Assert.AreSame(command, window.FindControl<MenuItem>("ResetOptionsMenuItem")!.Command);
            Activate(window, activation, "ResetOptionsButton", "ResetOptionsMenuItem", Key.R);
            var operation = command.ExecutionTask!;
            StringAssert.Contains(window.OwnedWindows.Single().Title!, "Checkout");
            Press(window, Key.R, ShortcutModifier);
            Assert.HasCount(1, window.OwnedWindows);
            RespondToConfirmation(window, confirm);
            await operation;
            Assert.AreEqual(confirm ? "" : "modified", model.Options.ItemProperties);
            Assert.AreEqual(!confirm, model.Options.NoBlock);
            Assert.AreEqual("N", model.Options.FeeAcknowledged);
            Assert.AreEqual(SipCommand.Checkout, model.SelectedCommand.Command);
            Assert.AreEqual("P123", model.Fields.PatronBarcode);
            Assert.AreEqual(SampleFields().ItemBarcode, model.Fields.ItemBarcode);
            Assert.AreEqual(SampleFields().Server, model.Fields.Server);
            model.SelectedCommand = Choice(SipCommand.Checkin);
            Assert.AreEqual("keep-other-command", model.Options.ReturnLocation);
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) { dialog.Close(); } window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(SipCommand.Checkout)]
    [DataRow(SipCommand.Checkin)]
    [DataRow(SipCommand.Renew)]
    [DataRow(SipCommand.RenewAll)]
    [DataRow(SipCommand.ItemInformation)]
    [DataRow(SipCommand.PatronInformation)]
    [DataRow(SipCommand.PatronStatus)]
    [DataRow(SipCommand.EndSession)]
    [DataRow(SipCommand.FeePaid)]
    [DataRow(SipCommand.NoticeUpdate)]
    [DataRow(SipCommand.Login)]
    [DataRow(SipCommand.AcsStatus)]
    public Task ResetDetectsEffectiveCommandDefaultsIncludingSeparateFields(SipCommand selected) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(selected) };
        model.Fields.Bp = "1";
        model.Fields.Bq = "5";
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Assert.IsFalse(model.HasModifiedOptions, selected.ToString());
            Press(window, Key.R, ShortcutModifier);
            var command = (IAsyncRelayCommand)window.FindControl<MenuItem>("ResetOptionsMenuItem")!.Command!;
            await command.ExecutionTask!;
            Assert.HasCount(0, window.OwnedWindows, "Unmodified options must reset without a dialog.");
            switch (selected)
            {
                case SipCommand.Checkout: model.Options.NoBlock = true; break;
                case SipCommand.Checkin: model.Options.ReturnLocation = "other"; break;
                case SipCommand.Renew: model.Options.DateText = "2030-12-01 12:00:00"; break;
                case SipCommand.RenewAll: model.Options.FeeAcknowledged = "Y"; break;
                case SipCommand.PatronInformation: model.SelectedSummary = SummaryChoice.All.Single(choice => choice.Key == "charged"); model.Fields.Bp = "2"; model.Fields.Bq = "9"; break;
                case SipCommand.PatronStatus: model.Options.LanguageCode = "001"; break;
                case SipCommand.FeePaid: model.Amount = "5.00"; model.TransactionId = "id"; break;
                case SipCommand.NoticeUpdate: model.SelectedNoticeMedium = (NoticeMedium)1; model.NoticeComment = "comment"; break;
                case SipCommand.Login: model.Options.UidAlgorithm = "1"; break;
                case SipCommand.AcsStatus: model.Options.PrintWidth = "80"; break;
                default: model.Options.TerminalPassword = "secret"; break;
            }
            Assert.IsTrue(model.HasModifiedOptions, selected.ToString());
            Press(window, Key.R, ShortcutModifier);
            var operation = command.ExecutionTask!;
            RespondToConfirmation(window, false);
            await operation;
            Assert.IsTrue(model.HasModifiedOptions, "Cancellation must preserve all modified values.");
            Press(window, Key.R, ShortcutModifier);
            operation = command.ExecutionTask!;
            RespondToConfirmation(window, true);
            await operation;
            Assert.IsFalse(model.HasModifiedOptions);
            Assert.AreEqual(selected, model.SelectedCommand.Command);
            Assert.AreEqual("P123", model.Fields.PatronBarcode);
            Assert.AreEqual(SampleFields().ItemBarcode, model.Fields.ItemBarcode);
            Assert.AreEqual(SampleFields().Username, model.Fields.Username);
            Assert.AreEqual(SampleFields().Port, model.Fields.Port);
            if (selected == SipCommand.PatronInformation)
            {
                Assert.AreEqual("none", model.SelectedSummary.Key);
                Assert.AreEqual("1", model.Fields.Bp);
                Assert.AreEqual("5", model.Fields.Bq);
            }
            model.IsBusy = true;
            Press(window, Key.R, ShortcutModifier);
            Assert.IsFalse(command.CanExecute(null));
            Assert.HasCount(0, window.OwnedWindows);
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) { dialog.Close(); } window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task InactiveOptionalDatesDoNotRequireResetConfirmation() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { SelectedCommand = Choice(SipCommand.Checkout) };
        model.Options.DateText = "an unused date";
        Assert.IsFalse(model.HasModifiedOptions);
        model.Options.UseDate = true;
        Assert.IsTrue(model.HasModifiedOptions);
        model.Options.UseDate = false;
        model.Options.FeeAcknowledged = "";
        Assert.IsTrue(model.HasModifiedOptions, "Omitting BO differs from the explicit default N.");
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(Key.X, "ClearLogMenuItem")]
    [DataRow(Key.R, "ResetOptionsMenuItem")]
    public Task HeldDestructiveShortcutsCannotReopenACancelledConfirmation(Key key, string name) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel();
        model.Options.ItemProperties = "modified";
        model.AppendTransaction(new() { Message = "99", Response = "98" });
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var modifiers = ShortcutModifier | (key == Key.X ? RawInputModifiers.Shift : RawInputModifiers.None);
            window.KeyPress(key, modifiers, PhysicalKey.None, null);
            var command = (IAsyncRelayCommand)window.FindControl<MenuItem>(name)!.Command!;
            var operation = command.ExecutionTask;
            RespondToConfirmation(window, false);
            await operation!;
            window.KeyPress(key, modifiers, PhysicalKey.None, null);
            Assert.HasCount(0, window.OwnedWindows, "A held key must stay suppressed after cancellation.");
            Assert.AreSame(operation, command.ExecutionTask);
            window.KeyRelease(key, modifiers, PhysicalKey.None, null);
            Press(window, key, modifiers);
            Assert.HasCount(1, window.OwnedWindows, "A fresh keypress must be allowed.");
            RespondToConfirmation(window, false);
            await command.ExecutionTask!;
            Assert.IsTrue(model.HasModifiedOptions && model.LogDocument.TextLength != 0);
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) { dialog.Close(); } window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task DestructiveDialogsFitBothThemesAndEscapeCancels(bool dark) => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields(), SelectedCommand = Choice(SipCommand.PatronInformation) };
        model.Preferences.DarkTheme = dark;
        model.Options.TerminalPassword = "modified";
        model.AppendTransaction(new() { Message = "99", Response = "98" });
        var window = new MainWindow { DataContext = model, Width = 1060, Height = 720 };
        window.Show();
        try
        {
            Layout(window);
            foreach (var (key, name, file) in new[] { (Key.R, "ResetOptionsMenuItem", "reset"), (Key.X, "ClearLogMenuItem", "clear") })
            {
                Press(window, key, ShortcutModifier | (key == Key.X ? RawInputModifiers.Shift : RawInputModifiers.None));
                var command = (IAsyncRelayCommand)window.FindControl<MenuItem>(name)!.Command!;
                var dialog = window.OwnedWindows.Single();
                Layout(dialog);
                Assert.AreEqual(dark ? ThemeVariant.Dark : ThemeVariant.Light, dialog.ActualThemeVariant);
                Assert.IsTrue(dialog.Bounds.Height < 300 && dialog.Bounds.Width <= window.Bounds.Width);
                foreach (var control in dialog.GetVisualDescendants().OfType<Button>())
                {
                    var position = control.TranslatePoint(default, dialog)!.Value;
                    Assert.IsTrue(position.X >= 0 && position.X + control.Bounds.Width <= dialog.Bounds.Width);
                }
                SaveFrame(dialog, $"shortcuts-{file}-confirmation-{(dark ? "dark" : "light")}");
                Press(dialog, Key.F6);
                Press(dialog, Key.F, ShortcutModifier);
                Assert.IsTrue(Field<Button>(dialog, "CancelButton").IsFocused);
                dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                await command.ExecutionTask!;
                Assert.IsTrue(model.HasModifiedOptions && model.LogDocument.TextLength != 0);
            }
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) { dialog.Close(); } window.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
