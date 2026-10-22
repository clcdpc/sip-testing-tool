using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Clc.Sip.Desktop.Models;

namespace Clc.Sip.Desktop.Views;

public sealed class OptionalDateTimeInput : UserControl
{
    public static readonly StyledProperty<string> DateTextProperty = AvaloniaProperty.Register<OptionalDateTimeInput, string>(nameof(DateText), "", defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> IsIncludedProperty = AvaloniaProperty.Register<OptionalDateTimeInput, bool>(nameof(IsIncluded), defaultBindingMode: BindingMode.TwoWay);
    public string DateText { get => GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }
    public bool IsIncluded { get => GetValue(IsIncludedProperty); set => SetValue(IsIncludedProperty, value); }
    private readonly TextBox editor;
    private readonly Avalonia.Controls.Calendar calendar = new();
    private readonly Flyout popup;
    private readonly Button calendarButton;
    private DateTime lastValid = DateTime.Now;

    public OptionalDateTimeInput()
    {
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*,32") };
        var include = new CheckBox { Name = "IncludeDate", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        AutomationProperties.SetName(include, "Include date/time");
        include.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(IsIncluded)) { Source = this, Mode = BindingMode.TwoWay });
        ToolTip.SetTip(include, "Include this date/time in the request.");
        editor = new TextBox { Name = "DateTimeEditor", PlaceholderText = CommandOptions.DateFormat };
        AutomationProperties.SetName(editor, "Date and time (yyyy-MM-dd HH:mm:ss)");
        editor.Bind(TextBox.TextProperty, new Binding(nameof(DateText)) { Source = this, Mode = BindingMode.TwoWay });
        editor.TextChanged += (_, _) =>
        {
            if (TryRead(out var date)) { lastValid = date; }
        };
        editor.AddHandler(InputElement.KeyDownEvent, EditorKeyDown, RoutingStrategies.Tunnel);
        calendarButton = new Button { Content = "▾", Padding = new Thickness(4), MinWidth = 0 };
        AutomationProperties.SetName(calendarButton, "Choose calendar date");
        popup = new Flyout { Content = calendar };
        calendarButton.Click += (_, _) => ShowCalendar();
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
        layout.Children.Add(include);
        layout.Children.Add(editor);
        layout.Children.Add(calendarButton);
        Content = layout;
    }

    public void FocusEditor() { editor.Focus(); editor.SelectAll(); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DateTextProperty && DateTime.TryParseExact(DateText, CommandOptions.DateFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { lastValid = date; }
    }

    private bool TryRead(out DateTime date) => DateTime.TryParseExact(editor.Text, CommandOptions.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private void ShowCalendar()
    {
        calendar.DisplayDate = lastValid.Date;
        popup.ShowAt(calendarButton);
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
