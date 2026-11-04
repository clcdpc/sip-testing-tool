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
    private static string MessageLogState(TextEditor log)
    {
        var view = log.TextArea.TextView;
        var scroll = (Avalonia.Controls.Primitives.IScrollable)view;
        var lines = view.VisualLinesValid
            ? string.Join(", ", view.VisualLines.Select(line => $"{line.FirstDocumentLine.LineNumber}:top={line.VisualTop:R},y={line.VisualTop - view.VerticalOffset:R},height={line.Height:R}"))
            : "invalid";
        var buttons = MessageExpanders(log);
        return $"valid={view.VisualLinesValid}; offset={view.VerticalOffset:R}; viewport={scroll.Viewport}; bounds={view.Bounds}; lineHeight={view.DefaultLineHeight:R}; extent={scroll.Extent}; lines=[{lines}]; expanders({buttons.Length})=[{string.Join(", ", buttons.Select(button => $"{AutomationProperties.GetName(button)}:y={Canvas.GetTop(button):R},focused={button.IsFocused}"))}]";
    }

    private static Button[] MessageExpanders(TextEditor log) => log.TextArea.LeftMargins.First()
        .GetVisualDescendants().OfType<Button>().OrderBy(button => button.TabIndex).ToArray();

    private static Button[] VisibleMessageExpanders(TextEditor log)
    {
        var buttons = MessageExpanders(log);
        var state = MessageLogState(log);
        Assert.IsTrue(log.TextArea.TextView.VisualLinesValid, "The rendered log should have valid visual lines. " + state);
        Assert.IsNotEmpty(buttons, "Visible message headers must have expanders. " + state);
        foreach (var button in buttons)
        {
            Assert.IsTrue(button.Focusable && button.IsTabStop, "Visible expanders must be keyboard accessible. " + state);
            Assert.IsTrue(button.Bounds.Bottom > 0 && button.Bounds.Top < log.TextArea.LeftMargins.First().Bounds.Height,
                "Every expander tab stop must intersect the viewport. " + state);
        }
        return buttons;
    }

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
            Press(window, Key.J, ShortcutModifier);
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
            Press(window, Key.J, ShortcutModifier);
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
    public Task MessagesShortcutAndActionsMenuReturnToOpenProperties(bool menu) => ui.Dispatch(() =>
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
                ActivateAction(window, "FocusMessagesMenuItem");
            }
            else
            {
                Press(window, Key.J, ShortcutModifier);
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
                Press(window, Key.Enter, ShortcutModifier);
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
            var last = VisibleMessageExpanders(log).Last();
            Assert.AreEqual("Expand sent message 80", AutomationProperties.GetName(last), MessageLogState(log));
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
            Assert.IsTrue(VisibleMessageExpanders(log).Any(button => button.IsFocused), MessageLogState(log));
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
            var initial = VisibleMessageExpanders(log);
            Assert.IsTrue(initial.Length > 1 && initial.Length < 80, MessageLogState(log));
            Assert.AreEqual("Expand sent message 1", AutomationProperties.GetName(initial[0]), MessageLogState(log));
            window.FindControl<Button>("ExportLogButton")!.Focus(NavigationMethod.Tab);
            foreach (var button in initial)
            {
                Press(window, Key.Tab);
                Assert.IsTrue(button.IsFocused, "Tab must follow the visible messages in document order. " + MessageLogState(log));
            }
            initial[0].Focus(NavigationMethod.Tab);
            log.ScrollToEnd();
            Layout(window);
            var buttons = VisibleMessageExpanders(log);
            Assert.IsTrue(buttons.Length < 80, "Only visible messages should create tab stops. " + MessageLogState(log));
            Assert.AreEqual("Expand sent message 80", AutomationProperties.GetName(buttons.Last()), MessageLogState(log));
            Assert.IsTrue(buttons[0].IsFocused, "Scrolling a focused expander to the end should focus a visible expander. " + MessageLogState(log));
            Assert.IsTrue(initial.All(button => button.GetVisualParent() == null), "Offscreen expanders must be removed.");

            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            Layout(window);
            log.ScrollToEnd();
            Layout(window);
            buttons = VisibleMessageExpanders(log);
            Assert.AreEqual("Expand sent message 80", AutomationProperties.GetName(buttons.Last()), MessageLogState(log));
            window.FindControl<Button>("ExportLogButton")!.Focus(NavigationMethod.Tab);
            Press(window, Key.Tab);
            Assert.IsTrue(buttons[0].IsFocused);
            log.ScrollToHome();
            Layout(window);
            var home = VisibleMessageExpanders(log);
            Assert.IsTrue(home[0].IsFocused, "Scrolling a focused expander out of view should focus a visible expander. " + MessageLogState(log));
            Assert.AreEqual("Expand sent message 1", AutomationProperties.GetName(home[0]), MessageLogState(log));
            Assert.IsTrue(buttons.All(button => button.GetVisualParent() == null), "Scrolling home must remove the end's expanders.");

            Press(window, Key.Enter);
            Layout(window);
            Assert.IsTrue(window.FindControl<MessageFieldsPanel>("MessageFields")!.IsVisible);
            window.LogInspection.Collapse();
            Layout(window);
            Assert.IsTrue(VisibleMessageExpanders(log)[0].IsFocused, MessageLogState(log));
            model.ClearLogCommand.Execute(null);
            Layout(window);
            Assert.IsEmpty(MessageExpanders(log), MessageLogState(log));
            Assert.IsTrue(window.FindControl<Button>("ClearLogButton")!.IsFocused, "Clearing the focused message should return focus to the log toolbar.");
            Press(window, Key.Tab);
            Assert.IsTrue(window.FindControl<Button>("ExportLogButton")!.IsFocused);
            Assert.IsFalse(log.IsKeyboardFocusWithin);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(0.0625)]
    [DataRow(0.5)]
    public Task PartiallyVisibleMessageHeadersKeepTheirExpanders(double hiddenFraction) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            // Keep the wrapped body in view after its header leaves the viewport.
            model.LogDocument.Text = "--> 9300CNuser|COpassword|" + string.Concat(Enumerable.Repeat("CPMAIN|", 2000)) + "\n\n<-- 941";
            Layout(window);
            var log = window.FindControl<TextEditor>("MessageLog")!;
            var first = VisibleMessageExpanders(log).Single();
            var height = first.Bounds.Height;
            var scroll = log.TextArea.GetVisualAncestors().OfType<ScrollViewer>().First();
            scroll.Offset = new Vector(0, height * hiddenFraction);
            Layout(window);
            var partial = VisibleMessageExpanders(log).Single();
            Assert.AreSame(first, partial, "Scrolling within a visible header should retain its control.");
            Assert.AreEqual("Expand sent message 1", AutomationProperties.GetName(partial), MessageLogState(log));
            Assert.IsTrue(Canvas.GetTop(partial) < 0, "The header must actually be partially clipped. " + MessageLogState(log));
            window.FindControl<Button>("ExportLogButton")!.Focus(NavigationMethod.Tab);
            Press(window, Key.Tab);
            Assert.IsTrue(partial.IsFocused, "A partially visible expander must remain reachable by Tab. " + MessageLogState(log));

            scroll.Offset = new Vector(0, height + 1);
            Layout(window);
            Assert.IsEmpty(MessageExpanders(log), "The wrapped body alone must not create an offscreen tab stop. " + MessageLogState(log));
            Assert.IsTrue(window.FindControl<Button>("ClearLogButton")!.IsFocused, "Without a visible header, focus should return to the log toolbar.");

            log.ScrollToHome();
            Layout(window);
            Assert.AreEqual("Expand sent message 1", AutomationProperties.GetName(VisibleMessageExpanders(log).Single()), MessageLogState(log));
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
