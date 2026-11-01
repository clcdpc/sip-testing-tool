using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task MessagesShortcutJumpsFromAnInputToAnExpanderOrTheEmptyLogToolbar(bool populated) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.Fields.Server = "preserve-server";
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            if (populated) { model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = "941" }); }
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var raw = log.Text;
            window.FindControl<TextBox>("ServerField")!.Focus();
            Press(window, Key.M, RawInputModifiers.Shift | (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control));
            Layout(window);
            if (populated)
            {
                var buttons = log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().OrderBy(button => button.TabIndex).ToArray();
                Assert.IsTrue(buttons[0].IsFocused);
                Press(window, Key.Tab);
                Assert.IsTrue(buttons[1].IsFocused);
            }
            else
            {
                Assert.IsTrue(window.FindControl<Button>("ClearLogButton")!.IsFocused);
            }
            Assert.IsFalse(log.TextArea.IsFocused);
            Assert.AreEqual("preserve-server", model.Fields.Server);
            Assert.AreEqual(raw, log.Text);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task MessagesShortcutBringsAWrappedMessagesExpanderBackIntoView() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.LogDocument.Text = "--> 9300CNuser|COpassword|\n<-- " + response + string.Concat(Enumerable.Repeat("AV25116288 $5.00|", 400));
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var raw = log.Text;
            log.ScrollToEnd();
            Layout(window);
            Assert.IsEmpty(log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>(), "The wrapped response's expander should initially be above the viewport.");
            window.FindControl<TextBox>("ServerField")!.Focus();
            Press(window, Key.M, RawInputModifiers.Shift | (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control));
            Layout(window);
            var focused = log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().Single(button => button.IsFocused);
            Assert.AreEqual("Expand received message 2", AutomationProperties.GetName(focused));
            Assert.IsFalse(log.TextArea.IsFocused);
            Assert.AreEqual(raw, log.Text);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task MessagesShortcutAndViewMenuReturnToOpenProperties(bool menu) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = "941" });
            Layout(window);
            window.LogInspection.ExpandMessageAtOffset(0);
            var panel = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            var table = panel.GetVisualDescendants().OfType<ListBox>().Single();
            Press(window, Key.Down);
            Assert.AreEqual(1, table.SelectedIndex);
            window.FindControl<TextBox>("ServerField")!.Focus();
            if (menu)
            {
                var view = window.GetVisualDescendants().OfType<MenuItem>().Single(item => Equals(item.Header, "_View"));
                view.IsSubMenuOpen = true;
                Layout(window);
                window.FindControl<MenuItem>("FocusMessagesMenuItem")!.Focus(NavigationMethod.Tab);
                Press(window, Key.Enter);
                Assert.IsFalse(view.IsSubMenuOpen);
            }
            else
            {
                Press(window, Key.M, RawInputModifiers.Shift | (OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control));
            }
            Layout(window);
            Assert.IsTrue(panel.IsVisible && table.IsKeyboardFocusWithin);
            Assert.AreEqual(1, table.SelectedIndex, "Jumping back should retain the selected property.");
            Press(window, Key.Escape);
            Assert.IsFalse(panel.IsVisible);
            Assert.IsFalse(window.FindControl<TextEditor>("MessageLog")!.TextArea.IsFocused);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task TabSkipsTheRawTranscriptAndReachesEveryVisibleExpander(bool populated) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            if (populated) { model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = "941" }); }
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var raw = log.Text;
            var buttons = log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().OrderBy(button => button.TabIndex).ToArray();
            var export = window.FindControl<Button>("ExportLogButton")!;
            export.Focus(NavigationMethod.Tab);
            Press(window, Key.Tab);
            foreach (var button in buttons)
            {
                Assert.IsTrue(button.IsFocused, "Tab should reach each expander without stopping in the read-only transcript.");
                Assert.IsFalse(log.TextArea.IsFocused);
                Press(window, Key.Tab);
            }
            Assert.IsFalse(log.IsKeyboardFocusWithin, "Tab should leave the log after its visible message expanders.");
            Press(window, Key.Tab, RawInputModifiers.Shift);
            foreach (var button in buttons.Reverse())
            {
                Assert.IsTrue(button.IsFocused, "Shift+Tab should also skip the transcript.");
                Press(window, Key.Tab, RawInputModifiers.Shift);
            }
            Assert.IsTrue(export.IsFocused);
            Assert.AreEqual(raw, log.Text);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task MessageExpandersSupportTabEnterSpaceAndAnArrowCursor(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = response });
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var raw = log.Text;
            var buttons = log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().OrderBy(button => button.TabIndex).ToArray();
            Assert.HasCount(2, buttons, "Each visible message needs a focusable expander.");
            Assert.AreEqual("Expand sent message 1", AutomationProperties.GetName(buttons[0]));
            Assert.AreEqual("Expand received message 2", AutomationProperties.GetName(buttons[1]));
            foreach (var button in buttons)
            {
                Assert.IsTrue(button.Focusable && button.IsTabStop);
                Assert.AreEqual(new Cursor(StandardCursorType.Arrow).ToString(), button.Cursor?.ToString());
            }

            log.Select(0, 3);
            window.FindControl<Button>("ExportLogButton")!.Focus(NavigationMethod.Tab);
            Press(window, Key.Tab);
            Assert.IsTrue(buttons[0].IsFocused);
            Press(window, Key.Enter);
            Layout(window);
            var panel = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            Assert.IsTrue(panel.IsVisible);
            Assert.AreEqual("Collapse sent message 1", AutomationProperties.GetName(buttons[0]));
            var table = panel.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.IsTrue(table.IsKeyboardFocusWithin, "Opening a message should focus its properties.");
            buttons[0].Focus(NavigationMethod.Tab);
            Press(window, Key.Space);
            Layout(window);
            Assert.IsFalse(panel.IsVisible);
            Assert.AreEqual("Expand sent message 1", AutomationProperties.GetName(buttons[0]));
            Press(window, Key.Tab, RawInputModifiers.Shift);
            Assert.IsTrue(window.FindControl<Button>("ExportLogButton")!.IsFocused);
            Press(window, Key.Tab);
            Press(window, Key.Tab);
            Assert.IsTrue(buttons[1].IsFocused);
            Press(window, Key.Space);
            Layout(window);
            Assert.IsTrue(panel.IsVisible);
            Assert.IsTrue(panel.Rows.Any(row => row.Field.Name.Contains("User1", StringComparison.Ordinal)));
            Assert.AreEqual("Collapse received message 2", AutomationProperties.GetName(buttons[1]));
            Assert.IsTrue(table.IsKeyboardFocusWithin, "Space should also focus the expanded properties.");
            SaveFrame(window, dark ? "dark-message-properties-focus" : "light-message-properties-focus");
            Assert.AreEqual(raw, log.Text);
            Assert.AreEqual("-->", log.SelectedText);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(2, false)]
    [DataRow(0, true)]
    [DataRow(2, true)]
    public Task ExpandingAnyMessageImmediatelyFocusesItsProperties(int messageIndex, bool layoutAfterExpansion) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            for (var i = 0; i < 2; i++) { model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = "941" }); }
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var raw = log.Text;
            var buttons = log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().OrderBy(button => button.TabIndex).ToArray();
            window.FindControl<Button>("ExportLogButton")!.Focus(NavigationMethod.Tab);
            for (var i = 0; i <= messageIndex; i++) { Press(window, Key.Tab); }
            Assert.IsTrue(buttons[messageIndex].IsFocused);
            Press(window, Key.Enter);
            if (layoutAfterExpansion) { Layout(window); }
            var panel = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            var table = panel.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.IsTrue(table.IsKeyboardFocusWithin, "Expanding any message must immediately focus its properties.");
            Assert.AreEqual(0, table.SelectedIndex);
            Press(window, Key.Down);
            Assert.AreEqual(1, table.SelectedIndex, "Arrow keys should browse the property rows.");
            Press(window, Key.Tab);
            Assert.IsFalse(table.IsKeyboardFocusWithin, "Tab should leave the properties using the normal tab order.");
            table.Focus(NavigationMethod.Tab);
            Press(window, Key.Tab, RawInputModifiers.Shift);
            Assert.IsFalse(table.IsKeyboardFocusWithin, "Shift+Tab should also leave the properties.");
            Press(window, Key.Escape);
            Assert.IsFalse(panel.IsVisible);
            Assert.IsTrue(buttons[messageIndex].IsFocused, "Closing properties should return focus to their expander.");
            Assert.AreEqual(raw, log.Text);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task MouseOrCaretExpansionImmediatelyFocusesProperties(bool mouse) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.AppendTransaction(new() { Message = "9300CNuser|COpassword|", Response = "941" });
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            if (mouse)
            {
                var button = log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().OrderBy(button => button.TabIndex).First();
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
            }
            else
            {
                log.CaretOffset = 0;
                log.TextArea.Focus();
                Press(window, Key.Enter, RawInputModifiers.Control);
            }
            var panel = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            var table = panel.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.IsTrue(table.IsKeyboardFocusWithin, "Opening with the mouse or Ctrl+Enter must immediately focus the properties.");
            Press(window, Key.Escape);
            Assert.IsFalse(panel.IsVisible);
            Assert.IsTrue(log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().First().IsFocused);
            Assert.IsFalse(log.TextArea.IsFocused);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task PropertiesRetainFocusWhenTheLogScrollsOrResizes() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.LogDocument.Text = string.Join("\n\n", Enumerable.Range(1, 80).Select(number => $"--> 9300CNuser{number}|COpassword|"));
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            log.ScrollToEnd();
            Layout(window);
            var last = log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().MaxBy(button => button.TabIndex)!;
            last.Focus(NavigationMethod.Tab);
            Press(window, Key.Space);
            Layout(window);
            var panel = window.FindControl<MessageFieldsPanel>("MessageFields")!;
            var table = panel.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.IsTrue(table.IsKeyboardFocusWithin, "Opening the properties should focus them even when the expander scrolls out of view.");
            log.ScrollToHome();
            Layout(window);
            Assert.IsTrue(table.IsKeyboardFocusWithin);
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            Layout(window);
            Assert.IsTrue(table.IsKeyboardFocusWithin);
            Press(window, Key.Down);
            Assert.AreEqual(1, table.SelectedIndex);
            SaveFrame(window, "message-properties-keyboard-focus");
            Press(window, Key.Escape);
            Assert.IsFalse(panel.IsVisible);
            Assert.IsTrue(log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().Any(button => button.IsFocused));
            Assert.IsFalse(log.TextArea.IsFocused);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task MessageExpandersFollowScrollingResizingAndClearingWithoutLosingFocus() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            model.LogDocument.Text = "Connection status\n\n" + string.Join("\n\n", Enumerable.Range(1, 80).Select(number => $"--> 9300CNuser{number}|COpassword|"));
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            Button[] Expanders() => log.TextArea.LeftMargins.First().GetVisualDescendants().OfType<Button>().OrderBy(button => button.TabIndex).ToArray();
            log.ScrollToEnd();
            Layout(window);
            var buttons = Expanders();
            Assert.IsTrue(buttons.Length > 0 && buttons.Length < 80, "Only visible messages should create tab stops.");
            Assert.AreEqual("Expand sent message 80", AutomationProperties.GetName(buttons.Last()));

            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            Layout(window);
            log.ScrollToEnd();
            Layout(window);
            buttons = Expanders();
            Assert.AreEqual("Expand sent message 80", AutomationProperties.GetName(buttons.Last()));
            window.FindControl<Button>("ExportLogButton")!.Focus(NavigationMethod.Tab);
            Press(window, Key.Tab);
            Assert.IsTrue(buttons[0].IsFocused);
            log.ScrollToHome();
            Layout(window);
            Assert.IsTrue(Expanders()[0].IsFocused, "Scrolling a focused expander out of view should focus a visible expander.");
            Assert.AreEqual("Expand sent message 1", AutomationProperties.GetName(Expanders()[0]));

            Press(window, Key.Enter);
            Layout(window);
            Assert.IsTrue(window.FindControl<MessageFieldsPanel>("MessageFields")!.IsVisible);
            window.LogInspection.Collapse();
            Layout(window);
            Assert.IsTrue(Expanders()[0].IsFocused);
            model.ClearLogCommand.Execute(null);
            Layout(window);
            Assert.IsEmpty(Expanders());
            Assert.IsTrue(window.FindControl<Button>("ClearLogButton")!.IsFocused, "Clearing the focused message should return focus to the log toolbar.");
            Press(window, Key.Tab);
            Assert.IsTrue(window.FindControl<Button>("ExportLogButton")!.IsFocused);
            Assert.IsFalse(log.IsKeyboardFocusWithin);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
