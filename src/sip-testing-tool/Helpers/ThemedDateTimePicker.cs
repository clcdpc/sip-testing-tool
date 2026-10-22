using System.ComponentModel;
using System.Globalization;

namespace Clc.Sip.Helpers;

/// <summary>An optional date/time editor whose field and calendar use the same palette.</summary>
public sealed class ThemedDateTimePicker : UserControl
{
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private readonly CheckBox includeDate;
    private readonly MaskedTextBox editor;
    private readonly ThemedButton calendarButton;
    private readonly ToolTip tips = new();
    private ToolStripDropDown popup;
    private DateCalendar calendar;
    private DateTime value = DateTime.Now;

    public ThemedDateTimePicker()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoScaleMode = AutoScaleMode.Inherit;
        Size = new Size(280, 24);
        TabStop = false;
        includeDate = new CheckBox
        {
            Name = "includeDateCheckBox",
            AutoSize = false,
            TabIndex = 0,
            AccessibleName = "Send no-block due date and time"
        };
        editor = new MaskedTextBox(@"0000-00-00 00\:00\:00")
        {
            Name = "dateTimeEditor",
            BorderStyle = BorderStyle.None,
            TabIndex = 1,
            AccessibleName = "No-block due date and time",
            AccessibleDescription = "Format: yyyy-MM-dd HH:mm:ss. Up and Down adjust the selected part; F4 opens the calendar."
        };
        calendarButton = new ThemedButton
        {
            Name = "calendarButton",
            Text = "▾",
            TabIndex = 2,
            AccessibleName = "Choose no-block due date",
            FlatStyle = FlatStyle.Flat
        };
        calendarButton.FlatAppearance.BorderSize = 0;
        Controls.AddRange([includeDate, editor, calendarButton]);
        includeDate.CheckedChanged += (_, _) =>
        {
            UpdateEditorAppearance();
            Invalidate();
        };
        editor.TextChanged += (_, _) =>
        {
            if (TryReadEditor(out var parsed))
            {
                value = parsed;
            }

            UpdateEditorAppearance();
        };
        editor.KeyDown += EditorKeyDown;
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Up or Keys.Down or Keys.Escape or Keys.F4)
            {
                e.IsInputKey = true;
            }
        };
        editor.Enter += (_, _) => Invalidate();
        editor.Leave += (_, _) => Invalidate();
        calendarButton.Click += (_, _) => ShowCalendar();
        Value = value;
        ApplyTheme();
    }

    [DefaultValue(false)]
    public bool Checked
    {
        get => includeDate.Checked; set => includeDate.Checked = value;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime Value
    {
        get => value;
        set
        {
            this.value = value;
            editor.Text = value.ToString(DateFormat, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Unchecked dates are omitted; a checked, incomplete date must never send an old value.</summary>
    public bool TryGetValue(out DateTime date) => TryReadEditor(out date) || !Checked;

    private bool TryReadEditor(out DateTime date)
    {
        date = default;
        return editor.MaskCompleted && DateTime.TryParseExact(editor.Text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public void FocusEditor()
    {
        editor.Focus();
        editor.SelectAll();
    }

    public void DescribeField(string name)
    {
        AccessibleName = name;
        includeDate.AccessibleName = "Use " + name;
        editor.AccessibleName = name;
        calendarButton.AccessibleName = "Choose " + name;
    }

    public void ApplyTheme()
    {
        BackColor = AppTheme.Input;
        ForeColor = AppTheme.Text;
        includeDate.BackColor = BackColor;
        includeDate.ForeColor = ForeColor;
        includeDate.FlatStyle = AppTheme.IsDarkMode && !SystemInformation.HighContrast ? FlatStyle.Flat : FlatStyle.Standard;
        includeDate.UseVisualStyleBackColor = false;
        editor.BackColor = BackColor;
        calendarButton.ForeColor = ForeColor;
        calendarButton.BackColor = AppTheme.Surface;
        calendarButton.FlatAppearance.MouseOverBackColor = AppTheme.Border;
        UpdateEditorAppearance();
        calendar?.ApplyTheme();
        if (popup != null)
        {
            popup.BackColor = AppTheme.Input;
            popup.ForeColor = AppTheme.Text;
        }
        Invalidate(true);
    }

    private void UpdateEditorAppearance()
    {
        var invalid = Checked && !TryReadEditor(out _);
        if (invalid)
        {
            editor.ForeColor = AppTheme.IsDarkMode ? Color.FromArgb(255, 166, 158) : Color.Firebrick;
        }
        else if (Checked)
        {
            editor.ForeColor = AppTheme.Text;
        }
        else
        {
            editor.ForeColor = SystemInformation.HighContrast || !AppTheme.IsDarkMode ? SystemColors.GrayText : Color.FromArgb(165, 174, 188);
        }
        tips.SetToolTip(editor, invalid ? "Enter a valid date and time: yyyy-MM-dd HH:mm:ss, or uncheck to omit it." : "Up/Down adjusts the selected part. F4 opens the calendar. Escape restores the last valid date.");
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (editor == null)
        {
            return;
        }

        var buttonWidth = LogicalToDeviceUnits(24);
        var checkWidth = LogicalToDeviceUnits(22);
        includeDate.SetBounds(3, 1, checkWidth - 3, Height - 2);
        editor.SetBounds(checkWidth, Math.Max(2, (Height - editor.PreferredHeight) / 2), Math.Max(1, Width - checkWidth - buttonWidth - 3), editor.PreferredHeight);
        calendarButton.SetBounds(Width - buttonWidth - 1, 1, buttonWidth, Height - 2);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Height = Math.Max(LogicalToDeviceUnits(24), Font.Height + LogicalToDeviceUnits(8));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Checked && !TryReadEditor(out _) ? Color.IndianRed : ContainsFocus ? SystemColors.Highlight : AppTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private void EditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F4 || e.Alt && e.KeyCode == Keys.Down)
        {
            ShowCalendar();
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Escape)
        {
            Value = value;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode is not (Keys.Up or Keys.Down) || !TryReadEditor(out var date))
        {
            return;
        }

        var caret = editor.SelectionStart;
        var step = e.KeyCode == Keys.Up ? 1 : -1;
        try
        {
            var partStart = caret switch
            {
                < 5 => 0,
                < 8 => 5,
                < 11 => 8,
                < 14 => 11,
                < 17 => 14,
                _ => 17
            };
            Value = partStart switch
            {
                0 => date.AddYears(step),
                5 => date.AddMonths(step),
                8 => date.AddDays(step),
                11 => date.AddHours(step),
                14 => date.AddMinutes(step),
                _ => date.AddSeconds(step)
            };
            Checked = true;
            editor.Select(partStart, partStart == 0 ? 4 : 2);
        }
        catch (ArgumentOutOfRangeException) { /* At the supported date boundary, leave the value unchanged. */ }
        e.SuppressKeyPress = true;
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData is Keys.F4 or (Keys.Alt | Keys.Down))
        {
            ShowCalendar();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    public void ShowCalendar()
    {
        if (popup?.Visible == true)
        {
            popup.Close();
            return;
        }
        popup?.Dispose();
        calendar = new DateCalendar(Value.Date, picked =>
        {
            Value = picked.Date.Add(Value.TimeOfDay);
            Checked = true;
            popup.Close();
            editor.Focus();
        })
        {
            Font = Font
        };
        var host = new ToolStripControlHost(calendar) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = calendar.Size };
        popup = new ToolStripDropDown { Padding = new Padding(1), BackColor = AppTheme.Input, AccessibleName = (AccessibleName ?? "Date and time") + " calendar" };
        popup.Items.Add(host);
        popup.Show(this, new Point(0, Height));
        calendar.FocusSelectedDay();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            popup?.Dispose();
            tips.Dispose();
        }
        base.Dispose(disposing);
    }
}
