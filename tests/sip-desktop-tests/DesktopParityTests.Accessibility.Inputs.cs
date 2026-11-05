using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilityCommandSelectionNotifiesAllTwelveValuesInBothDirections(bool expanded) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var picker = window.FindControl<ComboBox>("CommandPicker")!;
            picker.SelectedIndex = 0;
            picker.Focus();
            var peer = Peer(picker);
            var value = peer.GetProvider<IValueProvider>()!;
            var selection = peer.GetProvider<ISelectionProvider>()!;
            var expansion = peer.GetProvider<IExpandCollapseProvider>()!;
            AccessibilityAnnouncements.Announce(picker, "Test announcement");
            var region = Field<TextBlock>(window, "AccessibilityAnnouncement");
            var announcements = new List<string?>();
            Peer(region).PropertyChanged += (_, e) =>
            {
                if (e.Property == AutomationElementIdentifiers.NameProperty) { announcements.Add(e.NewValue as string); }
            };
            var changes = new List<AutomationPropertyChangedEventArgs>();
            peer.PropertyChanged += (_, e) => changes.Add(e);
            Assert.AreEqual(12, picker.ItemCount);
            Assert.AreEqual(CommandChoice.All[0].Name, value.Value);
            foreach (var direction in new[] { 1, -1 })
            {
                for (var step = 0; step < 11; step++)
                {
                    var index = picker.SelectedIndex + direction;
                    var previous = value.Value;
                    changes.Clear();
                    announcements.Clear();
                    if (expanded) { expansion.Expand(); Layout(window); }
                    Press(window, direction > 0 ? Key.Down : Key.Up);
                    if (expanded) { Press(window, Key.Enter); }
                    Layout(window);
                    Assert.AreEqual(index, picker.SelectedIndex);
                    Assert.AreSame(CommandChoice.All[index], model.SelectedCommand);
                    Assert.AreEqual(CommandChoice.All[index].Name, value.Value);
                    Assert.AreEqual(value.Value, selection.GetSelection().Single().GetName());
                    var notifications = changes.Where(e => e.Property == ValuePatternIdentifiers.ValueProperty).ToArray();
                    Assert.HasCount(1, notifications, "One value notification per committed selection.");
                    Assert.AreEqual(previous, notifications[0].OldValue);
                    Assert.AreEqual(value.Value, notifications[0].NewValue);
                    Assert.IsTrue(changes.Any(e => e.Property == SelectionPatternIdentifiers.SelectionProperty));
                    AssertNamed(picker, "SIP Command");
                    Assert.IsTrue(picker.IsFocused, "Navigation must retain/restore picker focus.");
                    Assert.AreEqual(ExpandCollapseState.Collapsed, expansion.ExpandCollapseState);
                    if (OperatingSystem.IsWindows() && !expanded)
                    {
                        CollectionAssert.AreEqual(new[] { value.Value }, announcements);
                    }
                    else { Assert.IsEmpty(announcements, "Expanded item focus must not get an extra live announcement."); }
                }
            }
            changes.Clear();
            Press(window, Key.Up);
            Assert.IsFalse(changes.Any(e => e.Property == ValuePatternIdentifiers.ValueProperty), "No duplicate at the list boundary.");
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilityFocusedValidationAnnouncesIdenticalAndChangedMessagesOnce(bool settings) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        Window window = settings ? new SettingsWindow() : new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            Control control = settings ? window.FindControl<AccessibleAutoCompleteBox>("EncodingField")! : window.FindControl<TextBox>("ServerField")!;
            var editor = control is AccessibleAutoCompleteBox autocomplete ? autocomplete.Editor! : control;
            var originalHelp = Peer(editor).GetHelpText();
            var announcements = RecordAnnouncements(window);
            var validation = new FieldValidationAccessibility(() => { });
            Assert.IsTrue(editor.Focus());
            foreach (var message in new[] { "First error.", "First error.", "Changed error.", "Changed error." })
            {
                var before = announcements.Count;
                validation.Show(control, message);
                Assert.IsTrue(editor.IsFocused);
                Assert.AreEqual(message, Peer(editor).GetHelpText());
                Assert.HasCount(before + (OperatingSystem.IsWindows() ? 1 : 0), announcements,
                    "Each attempt supplies exactly one nonempty live-region update.");
            }
            CollectionAssert.AreEqual(OperatingSystem.IsWindows()
                ? new[] { "First error.", "First error.", "Changed error.", "Changed error." }
                : Array.Empty<string>(), announcements);
            validation.Clear();
            Assert.AreEqual(originalHelp, Peer(editor).GetHelpText());
            Assert.HasCount(1, window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "AccessibilityAnnouncement"));
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityAnnouncementsReuseOneRegionAndRepeatIdenticalMessages() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var field = window.FindControl<TextBox>("ServerField")!;
            field.Focus();
            AccessibilityAnnouncements.Announce(field, "Password shown");
            var region = Field<TextBlock>(window, "AccessibilityAnnouncement");
            Assert.AreEqual(AutomationLiveSetting.Polite, Peer(region).GetLiveSetting());
            Assert.AreEqual(OperatingSystem.IsWindows(), region.IsVisible);
            region.IsVisible = true;
            Layout(window);
            var events = new List<string?>();
            Peer(region).PropertyChanged += (_, e) =>
            {
                if (e.Property == AutomationElementIdentifiers.NameProperty) { events.Add(e.NewValue as string); }
            };
            AccessibilityAnnouncements.Announce(field, "Password shown");
            AccessibilityAnnouncements.Announce(window.FindControl<TextBox>("PatronField")!, "Password hidden");
            CollectionAssert.AreEqual(new[] { "", "Password shown", "Password hidden" }, events);
            Assert.HasCount(1, window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "AccessibilityAnnouncement"));
            Assert.IsTrue(field.IsFocused);
            Assert.IsFalse(region.Focusable || region.IsHitTestVisible);
            Assert.AreEqual(1d, region.Bounds.Width);
            Assert.IsTrue(region.IsEffectivelyVisible);
            Assert.IsTrue(Peer(region).IsContentElement());
            Assert.AreEqual("Password hidden", Peer(region).GetName());
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityEveryStaticAndRebuiltChoiceUsesSelectionNotifications() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            var names = new HashSet<string>();
            foreach (var command in CommandChoice.All)
            {
                model.SelectedCommand = command;
                Layout(window);
                foreach (var combo in window.GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsEffectivelyVisible && c.Name != "CommandPicker").ToArray())
                {
                    Assert.IsInstanceOfType<AccessibleComboBox>(combo);
                    names.Add(Peer(combo).GetName());
                    combo.SelectedIndex = 0;
                    combo.Focus();
                    var values = new List<object?>();
                    Peer(combo).PropertyChanged += (_, e) =>
                    {
                        if (e.Property == ValuePatternIdentifiers.ValueProperty) { values.Add(e.NewValue); }
                    };
                    Press(window, Key.Down);
                    Assert.AreEqual(1, combo.SelectedIndex, command.Name);
                    Assert.HasCount(1, values);
                    Assert.AreEqual(Peer(combo).GetProvider<IValueProvider>()!.Value, values[0]);
                    Press(window, Key.Up);
                    Assert.AreEqual(0, combo.SelectedIndex);
                    Assert.HasCount(2, values);
                    Assert.IsTrue(combo.IsFocused);
                    combo.IsDropDownOpen = true;
                    Layout(window);
                    var item = (Control)combo.ContainerFromIndex(1)!;
                    var popup = TopLevel.GetTopLevel(item)!;
                    var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), popup)!.Value;
                    popup.MouseDown(point, MouseButton.Left);
                    popup.MouseUp(point, MouseButton.Left);
                    Layout(window);
                    Assert.AreEqual(1, combo.SelectedIndex, "Mouse selection: " + Peer(combo).GetName());
                    Assert.HasCount(3, values);
                    Assert.IsFalse(combo.IsDropDownOpen);
                    Assert.IsTrue(combo.IsFocused);
                }
            }
            foreach (var expected in new[] { "Summary", "Self-check status", "Delivery status", "Delivery medium", "Notice type", "Fee acknowledged (BO)", "Cancel (BI)" })
            {
                Assert.Contains(expected, names);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityEditableComboRetainsItsOriginalPatternsAndSingleTextNotification() => ui.Dispatch(() =>
    {
        var combo = new AccessibleComboBox { IsEditable = true, ItemsSource = new[] { "One", "Two" }, Text = "One" };
        var window = new Window { Content = combo };
        window.Show();
        try
        {
            Layout(window);
            var peer = Peer(combo);
            var changes = new List<object?>();
            peer.PropertyChanged += (_, e) =>
            {
                if (e.Property == ValuePatternIdentifiers.ValueProperty) { changes.Add(e.NewValue); }
            };
            var value = peer.GetProvider<IValueProvider>()!;
            Assert.IsFalse(value.IsReadOnly);
            value.SetValue("Edited");
            Assert.AreEqual("Edited", combo.Text);
            Assert.AreEqual("Edited", value.Value);
            CollectionAssert.AreEqual(new object?[] { "Edited" }, changes);
            Assert.IsNotNull(peer.GetProvider<IExpandCollapseProvider>());
            Assert.IsNotNull(peer.GetProvider<ISelectionProvider>());
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task AccessibilityPasswordFieldsMaskRevealAndHideWithoutChangingValues(bool dark) => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Password = "test-only-secret";
        model.Fields.PatronPin = "1234";
        model.Options.TerminalPassword = "terminal-test-secret";
        model.Preferences.DarkTheme = dark;
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var fields = window.GetVisualDescendants().OfType<PasswordTextBox>().ToArray();
            Assert.HasCount(3, fields);
            AccessibilityAnnouncements.Announce(fields[0], "Test announcement");
            var announcements = new List<string?>();
            Peer(Field<TextBlock>(window, "AccessibilityAnnouncement")).PropertyChanged += (_, e) =>
            {
                if (e.Property == AutomationElementIdentifiers.NameProperty) { announcements.Add(e.NewValue as string); }
            };
            foreach (var field in fields)
            {
                announcements.Clear();
                var peer = Peer(field);
                var original = field.Text;
                var name = peer.GetName();
                var status = new List<object?>();
                peer.PropertyChanged += (_, e) =>
                {
                    if (e.Property == AutomationElementIdentifiers.ItemStatusProperty) { status.Add(e.NewValue); }
                    Assert.AreNotEqual(original, e.NewValue, "No automation event may disclose a secret.");
                };
                Assert.AreNotEqual(default, field.PasswordChar);
                Assert.IsFalse(field.RevealPassword);
                Assert.AreEqual("password", peer.GetLocalizedControlType());
                StringAssert.Contains(peer.GetHelpText(), "Shift+H to show or hide");
                StringAssert.EndsWith(peer.GetItemStatus(), "hidden");
                field.Focus();
                field.CaretIndex = 2;
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                Assert.IsTrue(field.RevealPassword);
                Assert.AreEqual(2, field.CaretIndex);
                Assert.HasCount(1, status);
                StringAssert.EndsWith((string)status[0]!, "shown");
                if (OperatingSystem.IsWindows())
                {
                    CollectionAssert.AreEqual(new[] { field.IsPin ? "PIN shown" : "Password shown" }, announcements);
                }
                Assert.AreEqual(original, field.Text);
                Assert.AreEqual(new string(field.PasswordChar, original!.Length), peer.GetProvider<IValueProvider>()!.Value);
                Assert.IsTrue(fields.Where(other => other != field).All(other => !other.RevealPassword));
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                Assert.IsFalse(field.RevealPassword);
                Assert.HasCount(2, status);
                if (OperatingSystem.IsWindows())
                {
                    CollectionAssert.AreEqual(new[] { field.IsPin ? "PIN shown" : "Password shown", field.IsPin ? "PIN hidden" : "Password hidden" }, announcements);
                }
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                var eye = field.GetVisualDescendants().OfType<ToggleButton>().Single();
                AssertNamed(eye, field.IsPin ? "Show PIN" : "Show password");
                Assert.IsFalse(eye.Focusable, "Keep Fluent's minimal keyboard navigation.");
                Assert.IsFalse(Peer(eye).IsKeyboardFocusable());
                StringAssert.Contains(Peer(eye).GetHelpText(), "Shift+H to show or hide");
                var toggle = Peer(eye).GetProvider<IToggleProvider>()!;
                Assert.AreEqual(ToggleState.On, toggle.ToggleState);
                toggle.Toggle();
                Assert.IsFalse(field.RevealPassword);
                var point = eye.TranslatePoint(new Point(eye.Bounds.Width / 2, eye.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.IsTrue(field.RevealPassword, "Fluent eye must still work with the pointer.");
                Layout(window);
                Assert.IsTrue(field.IsFocused, "Pointer operation must retain focus in the password editor.");
                Assert.IsFalse(eye.Focus(), "The shortcut does not require a focusable eye.");
                Assert.IsTrue(field.RevealPassword);
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                Assert.IsFalse(field.RevealPassword, "Shortcut operates from the focused editor after a pointer reveal.");
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                window.FindControl<TextBox>("ServerField")!.Focus();
                Layout(window);
                Assert.IsFalse(field.RevealPassword);
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                field.Focus();
                Assert.IsFalse(field.RevealPassword);
                Assert.AreEqual(name, peer.GetName());
                Assert.AreEqual(original, field.Text);
                Assert.IsFalse(peer.GetHelpText().Contains(original, StringComparison.Ordinal));
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                window.FindControl<TextBox>("ServerField")!.Focus();
                field.Focus();
                Layout(window);
                Assert.IsFalse(field.RevealPassword, "A quick return must also start masked.");
            }
            Assert.AreEqual("test-only-secret", model.Fields.Password);
            Assert.AreEqual("1234", model.Fields.PatronPin);
            Assert.AreEqual("terminal-test-secret", model.Options.TerminalPassword);
            Assert.AreEqual(default, window.FindControl<TextBox>("UsernameField")!.PasswordChar);
            Assert.AreEqual(default, window.FindControl<TextBox>("PatronField")!.PasswordChar);
            Assert.AreEqual(default, window.FindControl<TextBox>("ItemField")!.PasswordChar);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityPasswordEditingAndRetemplatingKeepSecretsOutOfAutomationEvents() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Fields.Password = "test-only";
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var field = window.FindControl<PasswordTextBox>("PasswordField")!;
            var peer = Peer(field);
            var changes = new List<AutomationPropertyChangedEventArgs>();
            peer.PropertyChanged += (_, e) => changes.Add(e);
            field.Focus();
            Press(window, Key.End);
            window.KeyTextInput("-edited");
            Assert.AreEqual("test-only-edited", model.Fields.Password);
            var valueChange = changes.Single(e => e.Property == ValuePatternIdentifiers.ValueProperty);
            Assert.AreEqual(new string(field.PasswordChar, 9), valueChange.OldValue);
            Assert.AreEqual(new string(field.PasswordChar, 16), valueChange.NewValue);
            field.SelectAll();
            Assert.IsFalse(field.CanCopy, "Retain Avalonia's masked-field clipboard behavior.");
            Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
            Assert.IsTrue(field.CanCopy);
            Assert.AreEqual("test-only-edited", field.SelectedText);
            var template = field.Template;
            field.Template = null;
            field.ApplyTemplate();
            field.Template = template;
            Layout(window);
            var eye = field.GetVisualDescendants().OfType<ToggleButton>().Single();
            AssertNamed(eye, "Show password");
            Peer(eye).GetProvider<IToggleProvider>()!.Toggle();
            Assert.IsFalse(field.RevealPassword);
            Assert.AreEqual("test-only-edited", model.Fields.Password);
            Assert.AreEqual(new string(field.PasswordChar, field.Text!.Length), peer.GetProvider<IValueProvider>()!.Value);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityRebuiltPasswordOptionsRetainValuesAndResetReveal() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            foreach (var command in CommandChoice.All)
            {
                model.SelectedCommand = command;
                Layout(window);
                var field = window.GetVisualDescendants().OfType<PasswordTextBox>().SingleOrDefault(f => f.Name == "TerminalPasswordField");
                if (command.Command is SipCommand.Login or SipCommand.AcsStatus) { Assert.IsNull(field); continue; }
                Assert.IsNotNull(field);
                Assert.IsFalse(field.RevealPassword);
                field.Text = "option-test-secret";
                Assert.AreEqual("option-test-secret", model.Options.TerminalPassword);
                AssertNamed(field, "Terminal password (AC)");
                StringAssert.Contains(Peer(field).GetHelpText(), "Separate from login credentials");
                field.Focus();
                Press(window, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                Assert.IsTrue(field.RevealPassword);
                model.SelectedCommand = Choice(SipCommand.Login);
                Layout(window);
                Assert.IsFalse(field.RevealPassword, "Detached fields must mask immediately.");
                model.SelectedCommand = command;
                Layout(window);
                var replacement = Field<PasswordTextBox>(window, "TerminalPasswordField");
                Assert.AreNotSame(field, replacement);
                Assert.IsFalse(replacement.RevealPassword);
                Assert.AreEqual("option-test-secret", replacement.Text);
            }
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilitySettingsPasswordCancellationDiscardsEditsAndRevealState() => ui.Dispatch(async () =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        model.Preferences.Defaults.Password = "saved-test-secret";
        model.Preferences.Defaults.PatronPin = "5678";
        var owner = new MainWindow { DataContext = model };
        owner.Show();
        try
        {
            for (var pass = 0; pass < 2; pass++)
            {
                var settings = new SettingsWindow(model.Preferences);
                var result = settings.ShowDialog<AppPreferences?>(owner);
                Layout(settings);
                var fields = settings.GetVisualDescendants().OfType<PasswordTextBox>().ToArray();
                Assert.HasCount(2, fields);
                foreach (var field in fields)
                {
                    AssertNamed(field, field.IsPin ? "Patron PIN" : "Password");
                    Assert.AreEqual(field.IsPin ? "5678" : "saved-test-secret", field.Text);
                    Assert.IsFalse(field.RevealPassword);
                    field.Text = "unsaved-test-edit";
                    field.Focus();
                    Press(settings, Key.H, ShortcutModifier | RawInputModifiers.Shift);
                    Assert.IsTrue(field.RevealPassword);
                }
                settings.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                owner.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Assert.IsNull(await result);
                Layout(owner);
                Assert.IsTrue(fields.All(field => !field.RevealPassword));
                Assert.AreEqual("saved-test-secret", model.Preferences.Defaults.Password);
                Assert.AreEqual("5678", model.Preferences.Defaults.PatronPin);
            }
        }
        finally { owner.Close(); }
        return true;
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityExpandedInspectorSplitterNamesAndResizesBothPanels() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var splitter = window.FindControl<GridSplitter>("InspectorDivider")!;
            Assert.IsFalse(splitter.IsVisible);
            model.AppendTransaction(new() { Message = "9300CNtest-user|COtest-password|", Response = response });
            Layout(window);
            window.LogInspection.ExpandMessageAtOffset(model.LogDocument.GetLineByNumber(2).Offset);
            Layout(window);
            Assert.IsTrue(splitter.IsEffectivelyVisible);
            AssertNamed(splitter, "Resize message log and details");
            Assert.AreEqual("Use Up and Down Arrow keys to adjust panel heights.", Peer(splitter).GetHelpText());
            Assert.IsTrue(splitter.Focus());
            var grid = (Grid)splitter.Parent!;
            var logHeight = grid.RowDefinitions[0].ActualHeight;
            var detailsHeight = grid.RowDefinitions[2].ActualHeight;
            Press(window, Key.Up);
            Layout(window);
            Assert.IsTrue(grid.RowDefinitions[0].ActualHeight < logHeight);
            Assert.IsTrue(grid.RowDefinitions[2].ActualHeight > detailsHeight);
            Press(window, Key.Down);
            Layout(window);
            Assert.AreEqual(logHeight, grid.RowDefinitions[0].ActualHeight, 1);
            Assert.AreEqual(detailsHeight, grid.RowDefinitions[2].ActualHeight, 1);
            Assert.IsTrue(splitter.IsFocused);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    [TestMethod]
    public Task AccessibilityDescriptionsAndSplitterKeyboardResizeRemainUseful() => ui.Dispatch(() =>
    {
        using var model = new MainWindowViewModel { Fields = SampleFields() };
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            Layout(window);
            var shortcuts = new DesktopShortcuts(OperatingSystem.IsMacOS());
            foreach (var (name, gesture) in new[]
            {
                ("ServerField", shortcuts.FocusServer), ("PatronField", shortcuts.FocusPatron),
                ("ItemField", shortcuts.FocusItem), ("CommandPicker", shortcuts.FocusCommand),
                ("FindLogButton", shortcuts.FindLog), ("ClearLogButton", shortcuts.ClearLog),
                ("ExportLogButton", shortcuts.ExportLog), ("ResetOptionsButton", shortcuts.ResetOptions)
            })
            {
                Assert.AreEqual("Shortcut: " + DesktopShortcuts.Label(gesture), Peer(Field<Control>(window, name)).GetHelpText());
            }
            AssertNamed(window.FindControl<Button>("FindLogButton")!, "Find in message log");
            model.SelectedCommand = Choice(SipCommand.AcsStatus);
            Layout(window);
            Assert.AreEqual("Status reported by the self-check system.", Peer(Field<ComboBox>(window, "AcsStatusField")).GetHelpText());
            Assert.AreEqual("Number of characters, from 0 through 999.", Peer(Field<TextBox>(window, "PrintWidthField")).GetHelpText());
            Assert.AreEqual("Four characters in x.xx format.", Peer(Field<TextBox>(window, "ProtocolVersionField")).GetHelpText());
            model.SelectedCommand = Choice(SipCommand.Checkout);
            Layout(window);
            Assert.AreEqual("Choose Omit field to exclude this field from the SIP request.", Peer(Field<ComboBox>(window, "FeeAcknowledgedField")).GetHelpText());
            var splitter = window.FindControl<GridSplitter>("CommandMessageDivider")!;
            AssertNamed(splitter, "Resize command and message panels");
            Assert.AreEqual("Use Left and Right Arrow keys to adjust panel widths.", Peer(splitter).GetHelpText());
            Assert.IsTrue(splitter.Focus());
            var grid = (Grid)splitter.Parent!;
            var width = grid.ColumnDefinitions[0].ActualWidth;
            Press(window, Key.Right);
            Layout(window);
            Assert.IsTrue(grid.ColumnDefinitions[0].ActualWidth > width);
            Press(window, Key.Left);
            Layout(window);
            Assert.AreEqual(width, grid.ColumnDefinitions[0].ActualWidth, 1);
            Assert.IsTrue(splitter.IsFocused);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
}
