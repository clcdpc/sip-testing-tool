using System.Globalization;
using Avalonia;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Views;

namespace Clc.Sip.Desktop.Tests;

public sealed partial class DesktopParityTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task TimePickerOpensDirectlyWithAccessibleHoursMinutesAndSeconds(bool dark) => WithDateTimeInput((window, date) =>
    {
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Layout(window);
        AssertDateAccessibility(date, "Transaction Date");
        var picker = OpenTimePicker(date);
        Assert.AreEqual("24HourClock", picker.ClockIdentifier);
        Assert.IsTrue(picker.UseSeconds);
        Assert.AreEqual(1, picker.MinuteIncrement);
        Assert.AreEqual(1, picker.SecondIncrement);
        Assert.AreEqual(new TimeSpan(9, 15, 30), picker.Time);
        Assert.AreEqual("2026-11-04 09:15:30", date.DateText);
        Assert.IsFalse(date.IsIncluded);
        AssertNamed(picker, "Choose Transaction Date time");
        Assert.IsFalse(picker.GetVisualDescendants().OfType<TimePicker>().Any(), "The selectors must open without a second picker.");
        foreach (var (type, name, maximum) in new[]
        {
            (DateTimePickerPanelType.Hour, "Hours", 23),
            (DateTimePickerPanelType.Minute, "Minutes", 59),
            (DateTimePickerPanelType.Second, "Seconds", 59)
        })
        {
            var selector = TimeSelector(picker, type);
            AssertNamed(selector, name);
            Assert.AreEqual(0, selector.MinimumValue);
            Assert.AreEqual(maximum, selector.MaximumValue);
            Assert.AreEqual(1, selector.Increment);
            Assert.IsTrue(selector.IsEffectivelyVisible);
            Assert.IsTrue(AutomationDescendants(Peer(picker)).Contains(Peer(selector)));
            var selected = selector.Children.OfType<ListBoxItem>().Single(item => item.IsSelected);
            Assert.IsTrue(Peer(selected).GetProvider<ISelectionItemProvider>()!.IsSelected);
            AssertNamed(selected, selector.SelectedValue.ToString(type == DateTimePickerPanelType.Hour ? "0" : "00", CultureInfo.InvariantCulture));
        }
        AssertNamed(Field<Button>(picker, "PART_AcceptButton"), "Apply time");
        AssertNamed(Field<Button>(picker, "PART_DismissButton"), "Cancel time");
        Assert.IsTrue(TimeSelector(picker, DateTimePickerPanelType.Hour).IsFocused);
        SaveFrame(window, "date-time-field-" + (dark ? "dark" : "light"));
        SaveFrame(TopLevel.GetTopLevel(picker)!, "time-picker-" + (dark ? "dark" : "light"));
    });

    [TestMethod]
    [DataRow(0, 0, 0)]
    [DataRow(14, 45, 5)]
    [DataRow(23, 59, 59)]
    public Task TimePickerConfirmationPreservesDateAndUpdatesCommandOptionBindings(int hours, int minutes, int seconds) => WithDateTimeInput((_, date) =>
    {
        var options = new CommandOptions { DateText = date.DateText };
        date.Bind(OptionalDateTimeInput.DateTextProperty, new Binding(nameof(CommandOptions.DateText)) { Source = options, Mode = BindingMode.TwoWay });
        date.Bind(OptionalDateTimeInput.IsIncludedProperty, new Binding(nameof(CommandOptions.UseDate)) { Source = options, Mode = BindingMode.TwoWay });
        var picker = OpenTimePicker(date);
        TimeSelector(picker, DateTimePickerPanelType.Hour).SelectedValue = hours;
        TimeSelector(picker, DateTimePickerPanelType.Minute).SelectedValue = minutes;
        TimeSelector(picker, DateTimePickerPanelType.Second).SelectedValue = seconds;
        Assert.AreEqual("2026-11-04 09:15:30", options.DateText);
        Assert.IsFalse(options.UseDate);
        ClickDateTimeControl(Field<Button>(picker, "PART_AcceptButton"));
        var expected = new DateTime(2026, 11, 4, hours, minutes, seconds).ToString(CommandOptions.DateFormat, CultureInfo.InvariantCulture);
        Assert.AreEqual(expected, options.DateText);
        Assert.AreEqual(expected, date.Editor.Text);
        Assert.IsTrue(options.UseDate);
        Assert.IsFalse(TimeFlyout(date).IsOpen);
        Assert.IsTrue(Field<Button>(date, "TimeButton").IsFocused);
        options.DateText = "2030-06-01 03:04:05";
        picker = OpenTimePicker(date);
        Assert.AreEqual(new TimeSpan(3, 4, 5), picker.Time);
        PickerKey(picker, Key.Enter);
        Assert.AreEqual("2030-06-01 03:04:05", options.DateText, "Confirmation must preserve the external date and the binding.");
    });

    [TestMethod]
    [DataRow("button", false)]
    [DataRow("button", true)]
    [DataRow("escape", false)]
    [DataRow("escape", true)]
    [DataRow("outside", false)]
    [DataRow("outside", true)]
    [DataRow("unchanged", false)]
    [DataRow("unchanged", true)]
    public Task TimePickerCancellationPreservesValueAndReopeningDiscardsUnconfirmedChanges(string dismissal, bool included) => WithDateTimeInput((window, date) =>
    {
        date.IsIncluded = included;
        var picker = OpenTimePicker(date);
        if (dismissal != "unchanged")
        {
            TimeSelector(picker, DateTimePickerPanelType.Hour).SelectedValue = 23;
            TimeSelector(picker, DateTimePickerPanelType.Minute).SelectedValue = 59;
            TimeSelector(picker, DateTimePickerPanelType.Second).SelectedValue = 59;
        }
        if (dismissal == "button") { ClickDateTimeControl(Field<Button>(picker, "PART_DismissButton")); }
        else if (dismissal == "escape") { PickerKey(picker, Key.Escape); }
        else if (dismissal == "outside")
        {
            var outside = new Point(4, window.Bounds.Height - 4);
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
        }
        else { TimeFlyout(date).Hide(); }
        Layout(window);
        Assert.IsFalse(TimeFlyout(date).IsOpen);
        Assert.AreEqual("2026-11-04 09:15:30", date.DateText);
        Assert.AreEqual(included, date.IsIncluded);
        Assert.IsTrue(Field<Button>(date, "TimeButton").IsFocused);
        picker = OpenTimePicker(date);
        Assert.AreEqual(9, TimeSelector(picker, DateTimePickerPanelType.Hour).SelectedValue);
        Assert.AreEqual(15, TimeSelector(picker, DateTimePickerPanelType.Minute).SelectedValue);
        Assert.AreEqual(30, TimeSelector(picker, DateTimePickerPanelType.Second).SelectedValue);
    });

    [TestMethod]
    [DataRow("")]
    [DataRow("2026-11-")]
    [DataRow("invalid")]
    public Task TimePickerUsesLastValidManualEntryWithoutReplacingInvalidTextOnOpen(string invalid) => WithDateTimeInput((window, date) =>
    {
        date.FocusEditor();
        window.KeyTextInput("2030-06-01 03:04:05");
        Assert.AreEqual("2030-06-01 03:04:05", date.DateText);
        date.Editor.SelectAll();
        window.KeyTextInput(invalid);
        if (invalid.Length == 0) { Press(window, Key.Back); }
        var picker = OpenTimePicker(date);
        Assert.AreEqual(new TimeSpan(3, 4, 5), picker.Time);
        Assert.AreEqual(invalid, date.DateText);
        Assert.IsFalse(date.IsIncluded);
        PickerKey(picker, Key.Escape);
        Assert.AreEqual(invalid, date.DateText);
        picker = OpenTimePicker(date);
        TimeSelector(picker, DateTimePickerPanelType.Second).SelectedValue = 6;
        PickerKey(picker, Key.Enter);
        Assert.AreEqual("2030-06-01 03:04:06", date.DateText);
        Assert.IsTrue(date.IsIncluded);
    });

    [TestMethod]
    [DataRow("")]
    [DataRow("2026-11-")]
    public Task TimePickerOpeningAndDismissingPreservesInitiallyEmptyOrPartialText(string initial) => WithDateTimeInput((window, _) =>
    {
        var fresh = new OptionalDateTimeInput { DateText = initial };
        ((StackPanel)window.Content!).Children.Add(fresh);
        Layout(window);
        var picker = OpenTimePicker(fresh);
        Assert.AreEqual(initial, fresh.DateText);
        Assert.IsFalse(fresh.IsIncluded);
        PickerKey(picker, Key.Escape);
        Assert.AreEqual(initial, fresh.DateText);
        Assert.IsFalse(fresh.IsIncluded);
    });

    [TestMethod]
    [DataRow(Key.F4, RawInputModifiers.None)]
    [DataRow(Key.Down, RawInputModifiers.Alt)]
    public Task TimePickerAndCalendarSelectionsPreserveEachOthersComponents(Key shortcut, RawInputModifiers modifiers) => WithDateTimeInput((window, date) =>
    {
        var picker = OpenTimePicker(date);
        TimeSelector(picker, DateTimePickerPanelType.Hour).SelectedValue = 14;
        TimeSelector(picker, DateTimePickerPanelType.Minute).SelectedValue = 45;
        TimeSelector(picker, DateTimePickerPanelType.Second).SelectedValue = 5;
        PickerKey(picker, Key.Enter);
        date.Editor.Focus();
        Press(window, shortcut, modifiers);
        Layout(window);
        var popup = (Flyout)typeof(OptionalDateTimeInput).GetField("popup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(date)!;
        Assert.IsTrue(popup.IsOpen);
        ((Avalonia.Controls.Calendar)popup.Content!).SelectedDate = new DateTime(2026, 11, 10);
        Assert.AreEqual("2026-11-10 14:45:05", date.DateText);
        Assert.IsTrue(date.Editor.IsFocused);
        picker = OpenTimePicker(date);
        Assert.AreEqual(new TimeSpan(14, 45, 5), picker.Time);
    });

    [TestMethod]
    public Task TimePickerControlsMaintainIndependentSelectionState() => WithDateTimeInput((window, first) =>
    {
        var second = new OptionalDateTimeInput { DateText = "2030-06-01 03:04:05" };
        ((StackPanel)window.Content!).Children.Add(second);
        Layout(window);
        var picker = OpenTimePicker(first);
        TimeSelector(picker, DateTimePickerPanelType.Hour).SelectedValue = 14;
        PickerKey(picker, Key.Enter);
        picker = OpenTimePicker(second);
        Assert.AreEqual(new TimeSpan(3, 4, 5), picker.Time);
        TimeSelector(picker, DateTimePickerPanelType.Second).SelectedValue = 59;
        PickerKey(picker, Key.Escape);
        Assert.AreEqual("2030-06-01 03:04:05", second.DateText);
        Assert.IsFalse(second.IsIncluded);
        picker = OpenTimePicker(first);
        Assert.AreEqual(new TimeSpan(14, 15, 30), picker.Time);
        Assert.AreEqual("2026-11-04 14:15:30", first.DateText);
        Assert.IsTrue(first.IsIncluded);
    });

    [TestMethod]
    [DataRow(Key.Enter)]
    [DataRow(Key.Space)]
    public Task TimePickerSupportsTabActivationSelectorNavigationAndKeyboardConfirmation(Key activation) => WithDateTimeInput((window, date) =>
    {
        date.Editor.Focus();
        Press(window, Key.Tab);
        Assert.IsTrue(Field<Button>(date, "CalendarButton").IsFocused);
        Press(window, Key.Tab);
        Assert.IsTrue(Field<Button>(date, "TimeButton").IsFocused);
        Press(window, activation);
        Layout(window);
        Assert.IsTrue(TimeFlyout(date).IsOpen);
        var picker = (TimePickerPresenter)TimeFlyout(date).Content!;
        Layout(TopLevel.GetTopLevel(picker)!);
        foreach (var type in new[] { DateTimePickerPanelType.Hour, DateTimePickerPanelType.Minute, DateTimePickerPanelType.Second })
        {
            Assert.IsTrue(TimeSelector(picker, type).IsFocused, type.ToString());
            PickerKey(picker, Key.Down);
            if (type != DateTimePickerPanelType.Second) { PickerKey(picker, Key.Tab); }
        }
        PickerKey(picker, Key.Enter);
        Assert.AreEqual("2026-11-04 10:16:31", date.DateText);
        Assert.IsTrue(date.IsIncluded);
        Assert.IsTrue(Field<Button>(date, "TimeButton").IsFocused);
        Press(window, Key.Tab, RawInputModifiers.Shift);
        Assert.IsTrue(Field<Button>(date, "CalendarButton").IsFocused);
    });

    [TestMethod]
    [DataRow(0, "2027-11-04 09:15:30")]
    [DataRow(5, "2026-12-04 09:15:30")]
    [DataRow(8, "2026-11-05 09:15:30")]
    [DataRow(11, "2026-11-04 10:15:30")]
    [DataRow(14, "2026-11-04 09:16:30")]
    [DataRow(17, "2026-11-04 09:15:31")]
    public Task DateTimeComponentIncrementAndEscapeBehaviorRemainIntact(int caret, string expected) => WithDateTimeInput((window, date) =>
    {
        date.Editor.Focus();
        date.Editor.SelectionStart = caret;
        date.Editor.SelectionEnd = caret;
        Press(window, Key.Up);
        Assert.AreEqual(expected, date.DateText);
        Assert.IsTrue(date.IsIncluded);
        Assert.AreEqual(caret, date.Editor.SelectionStart);
        Assert.AreEqual(caret + (caret == 0 ? 4 : 2), date.Editor.SelectionEnd);
        Press(window, Key.Down);
        Assert.AreEqual("2026-11-04 09:15:30", date.DateText);
        date.Editor.Text = "partial";
        Press(window, Key.Escape);
        Assert.AreEqual("2026-11-04 09:15:30", date.DateText);
    });

    private static Task WithDateTimeInput(Action<Window, OptionalDateTimeInput> check) => ui.Dispatch(() =>
    {
        var date = new OptionalDateTimeInput { DateText = "2026-11-04 09:15:30", VerticalAlignment = VerticalAlignment.Top };
        var row = new OptionalFieldRow { Label = "Transaction Date", Field = date };
        var panel = new StackPanel { Margin = new Thickness(12), Children = { row } };
        var window = new Window { Width = 420, Height = 540, Content = panel };
        window.Show();
        try
        {
            Layout(window);
            check(window, date);
        }
        finally { window.Close(); }
    }, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

    private static Flyout TimeFlyout(OptionalDateTimeInput date) => (Flyout)Field<Button>(date, "TimeButton").Flyout!;

    private static TimePickerPresenter OpenTimePicker(OptionalDateTimeInput date)
    {
        ClickDateTimeControl(Field<Button>(date, "TimeButton"));
        Assert.IsTrue(TimeFlyout(date).IsOpen);
        var picker = (TimePickerPresenter)TimeFlyout(date).Content!;
        Layout(TopLevel.GetTopLevel(picker)!);
        return picker;
    }

    private static DateTimePickerPanel TimeSelector(TimePickerPresenter picker, DateTimePickerPanelType type) =>
        picker.GetVisualDescendants().OfType<DateTimePickerPanel>().Single(panel => panel.PanelType == type);

    private static void ClickDateTimeControl(Control target)
    {
        var root = TopLevel.GetTopLevel(target)!;
        Layout(root);
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root)!.Value;
        root.MouseDown(point, MouseButton.Left);
        root.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void PickerKey(TimePickerPresenter picker, Key key)
    {
        var root = TopLevel.GetTopLevel(picker)!;
        var physical = key == Key.Enter ? PhysicalKey.Enter : key == Key.Tab ? PhysicalKey.Tab : PhysicalKey.None;
        root.KeyPress(key, RawInputModifiers.None, physical, null);
        root.KeyRelease(key, RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();
    }
}
