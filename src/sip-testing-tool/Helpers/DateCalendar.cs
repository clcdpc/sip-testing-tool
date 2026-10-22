using System.Globalization;

namespace Clc.Sip.Helpers;

internal sealed class DateCalendar : UserControl
{
    private readonly TableLayoutPanel grid;
    private readonly Label monthTitle;
    private readonly Button previous;
    private readonly Button next;
    private readonly Button today;
    private readonly List<Button> days = [];
    private readonly Action<DateTime> picked;
    private readonly DateTime selected;
    private DateTime month;

    public DateCalendar(DateTime selected, Action<DateTime> picked)
    {
        this.selected = selected;
        this.picked = picked;
        month = new DateTime(selected.Year, selected.Month, 1);
        Name = "dateCalendar";
        AccessibleName = "Choose a date";
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(280, 286);
        grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 9, Padding = new Padding(6) };
        for (var column = 0; column < 7; column++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        for (var row = 0; row < 6; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6));
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(grid);
        previous = MakeButton("‹", "Previous month", (_, _) => ChangeMonth(-1));
        next = MakeButton("›", "Next month", (_, _) => ChangeMonth(1));
        monthTitle = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false };
        grid.Controls.Add(previous, 0, 0);
        grid.Controls.Add(monthTitle, 1, 0);
        grid.SetColumnSpan(monthTitle, 5);
        grid.Controls.Add(next, 6, 0);
        var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        for (var column = 0; column < 7; column++)
        {
            var name = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[((int)firstDay + column) % 7];
            grid.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter }, column, 1);
        }
        for (var index = 0; index < 42; index++)
        {
            var button = MakeButton("", "", (sender, _) => picked((DateTime)((Button)sender).Tag));
            button.KeyDown += DayKeyDown;
            button.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End)
                {
                    e.IsInputKey = true;
                }
            };
            days.Add(button);
            grid.Controls.Add(button, index % 7, 2 + index / 7);
        }
        today = MakeButton("Today: " + DateTime.Today.ToString("d"), "Choose today", (_, _) => picked(DateTime.Today));
        grid.Controls.Add(today, 0, 8);
        grid.SetColumnSpan(today, 7);
        PopulateDays();
    }

    private static Button MakeButton(string text, string name, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AccessibleName = name,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(1),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private void PopulateDays()
    {
        monthTitle.Text = month.ToString("MMMM yyyy");
        var offset = ((int)month.DayOfWeek - (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek + 7) % 7;
        var firstTicks = month.Ticks - (long)offset * TimeSpan.TicksPerDay;
        var selectionVisible = selected.Ticks >= firstTicks && selected.Ticks < firstTicks + 42L * TimeSpan.TicksPerDay;
        for (var index = 0; index < days.Count; index++)
        {
            var ticks = month.Ticks + (long)(index - offset) * TimeSpan.TicksPerDay;
            var button = days[index];
            button.Enabled = ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks;
            if (!button.Enabled)
            {
                button.Text = "";
                button.Tag = null;
                continue;
            }
            var date = new DateTime(ticks);
            button.Tag = date;
            button.Name = "calendarDay-" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            button.Text = date.Day.ToString(CultureInfo.CurrentCulture);
            button.AccessibleName = date.ToString("D");
            button.TabStop = date == selected || date == month && !selectionVisible;
        }
        previous.Enabled = month.Year > 1 || month.Month > 1;
        next.Enabled = month.Year < 9999 || month.Month < 12;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        BackColor = grid.BackColor = AppTheme.Input;
        ForeColor = grid.ForeColor = AppTheme.Text;
        monthTitle.ForeColor = AppTheme.Text;
        foreach (var button in days.Concat([previous, next, today]))
        {
            var date = button.Tag as DateTime?;
            var chosen = date == selected;
            button.BackColor = chosen ? SystemColors.Highlight : AppTheme.Input;
            button.ForeColor = chosen ? SystemColors.HighlightText : date.HasValue && date.Value.Month != month.Month
                ? SystemInformation.HighContrast ? SystemColors.GrayText : AppTheme.IsDarkMode ? Color.FromArgb(165, 174, 188) : Color.DimGray : AppTheme.Text;
            button.FlatAppearance.MouseOverBackColor = chosen ? SystemColors.Highlight : AppTheme.Surface;
            button.FlatAppearance.BorderSize = date == DateTime.Today ? 1 : 0;
            button.FlatAppearance.BorderColor = SystemColors.Highlight;
        }
        Invalidate(true);
    }

    private void ChangeMonth(int step)
    {
        try
        {
            month = month.AddMonths(step);
            PopulateDays();
        }
        catch (ArgumentOutOfRangeException) { }
    }

    public void FocusSelectedDay() => (days.FirstOrDefault(day => day.Tag is DateTime date && date == selected)
        ?? days.First(day => day.Tag is DateTime date && date == month)).Focus();

    private void DayKeyDown(object sender, KeyEventArgs e)
    {
        var date = (DateTime)((Button)sender).Tag;
        DateTime target;
        try
        {
            target = e.KeyCode switch
            {
                Keys.Left => date.AddDays(-1),
                Keys.Right => date.AddDays(1),
                Keys.Up => date.AddDays(-7),
                Keys.Down => date.AddDays(7),
                Keys.PageUp => date.AddMonths(-1),
                Keys.PageDown => date.AddMonths(1),
                Keys.Home => new DateTime(date.Year, date.Month, 1),
                Keys.End => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
                _ => date
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            e.SuppressKeyPress = true;
            return;
        }
        if (target == date)
        {
            return;
        }

        month = new DateTime(target.Year, target.Month, 1);
        PopulateDays();
        days.First(day => day.Tag is DateTime d && d == target).Focus();
        e.SuppressKeyPress = true;
    }
}
