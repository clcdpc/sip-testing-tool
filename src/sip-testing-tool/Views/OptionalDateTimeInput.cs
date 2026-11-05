using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Clc.Sip.Desktop.Models;

namespace Clc.Sip.Desktop.Views;

public sealed class OptionalDateTimeInput : UserControl
{
    public static readonly StyledProperty<string> DateTextProperty = AvaloniaProperty.Register<OptionalDateTimeInput, string>(nameof(DateText), "", defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> IsIncludedProperty = AvaloniaProperty.Register<OptionalDateTimeInput, bool>(nameof(IsIncluded), defaultBindingMode: BindingMode.TwoWay);
    public string DateText { get => GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }
    public bool IsIncluded { get => GetValue(IsIncludedProperty); set => SetValue(IsIncludedProperty, value); }
    private readonly TextBox editor;
    private readonly CheckBox include;
    internal TextBox Editor => editor;
    private readonly Avalonia.Controls.Calendar calendar = new();
    private readonly Flyout popup;
    private readonly Button calendarButton;
    private readonly Flyout timePopup;
    private readonly Button timeButton;
    private DateTime lastValid = DateTime.Now;

    public OptionalDateTimeInput()
    {
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*,26,26") };
        include = new CheckBox { Name = "IncludeDate", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(0) };
        AutomationProperties.SetName(include, "Include date/time");
        include.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(IsIncluded)) { Source = this, Mode = BindingMode.TwoWay });
        ToolTip.SetTip(include, "Include this date/time in the request.");
        editor = new TextBox { Name = "DateTimeEditor", PlaceholderText = CommandOptions.DateFormat, BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinHeight = 24, Padding = new Thickness(4, 1) };
        AutomationProperties.SetName(editor, "Date and time (yyyy-MM-dd HH:mm:ss)");
        editor.Bind(TextBox.TextProperty, new Binding(nameof(DateText)) { Source = this, Mode = BindingMode.TwoWay });
        editor.TextChanged += (_, _) =>
        {
            if (TryRead(out var date)) { lastValid = date; }
        };
        editor.AddHandler(InputElement.KeyDownEvent, EditorKeyDown, RoutingStrategies.Tunnel);
        var chevron = new PathIcon { Width = 10, Height = 6 };
        AutomationProperties.SetAccessibilityView(chevron, AccessibilityView.Raw);
        chevron.Bind(PathIcon.DataProperty, new DynamicResourceExtension("DropDownChevron"));
        calendarButton = new Button { Name = "CalendarButton", Content = chevron, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinWidth = 0, MinHeight = 24, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        AutomationProperties.SetName(calendarButton, "Choose calendar date");
        popup = new Flyout { Content = calendar };
        calendarButton.Click += (_, _) => ShowCalendar();
        var clock = new PathIcon { Width = 12, Height = 12 };
        AutomationProperties.SetAccessibilityView(clock, AccessibilityView.Raw);
        clock.Bind(PathIcon.DataProperty, new DynamicResourceExtension("ClockIcon"));
        timePopup = new Flyout();
        timeButton = new Button { Name = "TimeButton", Content = clock, Flyout = timePopup, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinWidth = 0, MinHeight = 24, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        ToolTip.SetTip(timeButton, "Choose time");
        timePopup.Opening += (_, _) => PrepareTimePicker();
        timePopup.Closed += (_, _) => timeButton.Focus();
        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is not { } date) { return; }
            SetCurrentValue(DateTextProperty, date.Date.Add(lastValid.TimeOfDay).ToString(CommandOptions.DateFormat, CultureInfo.InvariantCulture));
            SetCurrentValue(IsIncludedProperty, true);
            popup.Hide();
            editor.Focus();
        };
        Grid.SetColumn(editor, 1);
        Grid.SetColumn(calendarButton, 2);
        Grid.SetColumn(timeButton, 3);
        layout.Children.Add(include);
        layout.Children.Add(editor);
        layout.Children.Add(calendarButton);
        layout.Children.Add(timeButton);
        var border = new Border { Child = layout, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3) };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("LineBrush"));
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("InputBrush"));
        Content = border;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        UpdateAccessibleNames();
    }

    public void FocusEditor() { editor.Focus(); editor.SelectAll(); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AutomationProperties.NameProperty || change.Property == AutomationProperties.LabeledByProperty) { UpdateAccessibleNames(); }
        if (change.Property == DateTextProperty && DateTime.TryParseExact(DateText, CommandOptions.DateFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { lastValid = date; }
    }

    internal void UpdateAccessibleNames()
    {
        if (editor == null) { return; }
        var label = AutomationProperties.GetName(this);
        if (string.IsNullOrWhiteSpace(label) && AutomationProperties.GetLabeledBy(this) is TextBlock caption) { label = caption.Text; }
        if (string.IsNullOrWhiteSpace(label)) { label = "Date and time"; }
        AutomationProperties.SetName(editor, label + " (" + CommandOptions.DateFormat + ")");
        AutomationProperties.SetName(include, "Include " + label);
        AutomationProperties.SetName(calendarButton, "Choose " + label + " in calendar");
        AutomationProperties.SetName(timeButton, "Choose " + label + " time");
    }

    private bool TryRead(out DateTime date) => DateTime.TryParseExact(editor.Text, CommandOptions.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private void ShowCalendar()
    {
        calendar.DisplayDate = lastValid.Date;
        popup.ShowAt(calendarButton);
    }

    private void PrepareTimePicker()
    {
        // A fresh presenter also resets unconfirmed selector changes when reopened.
        var picker = new TimePickerPresenter
        {
            ClockIdentifier = "24HourClock",
            UseSeconds = true,
            MinuteIncrement = 1,
            SecondIncrement = 1,
            Time = lastValid.TimeOfDay
        };
        AutomationProperties.SetName(picker, AutomationProperties.GetName(timeButton));
        picker.Confirmed += (_, _) =>
        {
            SetCurrentValue(DateTextProperty, lastValid.Date.Add(picker.Time).ToString(CommandOptions.DateFormat, CultureInfo.InvariantCulture));
            SetCurrentValue(IsIncludedProperty, true);
            timePopup.Hide();
        };
        picker.Dismissed += (_, _) => timePopup.Hide();
        timePopup.Content = picker;
    }

    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F4 || (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt))
        {
            ShowCalendar();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SetCurrentValue(DateTextProperty, lastValid.ToString(CommandOptions.DateFormat, CultureInfo.InvariantCulture));
            e.Handled = true;
        }
        else if (e.Key is Key.Up or Key.Down && TryRead(out var date))
        {
            var caret = Math.Min(editor.SelectionStart, editor.SelectionEnd);
            var start = caret < 5 ? 0 : caret < 8 ? 5 : caret < 11 ? 8 : caret < 14 ? 11 : caret < 17 ? 14 : 17;
            var step = e.Key == Key.Up ? 1 : -1;
            try
            {
                date = start switch { 0 => date.AddYears(step), 5 => date.AddMonths(step), 8 => date.AddDays(step), 11 => date.AddHours(step), 14 => date.AddMinutes(step), _ => date.AddSeconds(step) };
                SetCurrentValue(DateTextProperty, date.ToString(CommandOptions.DateFormat, CultureInfo.InvariantCulture));
                SetCurrentValue(IsIncludedProperty, true);
                editor.SelectionStart = start;
                editor.SelectionEnd = start + (start == 0 ? 4 : 2);
            }
            catch (ArgumentOutOfRangeException) { }
            e.Handled = true;
        }
    }
}
