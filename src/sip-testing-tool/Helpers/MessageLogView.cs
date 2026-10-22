namespace Clc.Sip.Helpers;

// Keeps the transcript as a native RichTextBox so selection, hover help and export
// retain their original behavior. The gutter follows its visual line positions.
public sealed class MessageLogView : UserControl
{
    private sealed record Message(int Offset, bool Sent, string Text, int Number);

    private readonly RichTextBox log;
    private readonly SplitContainer split;
    private readonly Panel gutter;
    private readonly Label heading;
    private readonly DataGridView grid;
    private readonly Font boldFont;
    private readonly Dictionary<int, Button> buttons = new();
    private readonly List<Message> messages = new();
    private string transcript = "";
    private int expandedOffset = -1;
    private bool refreshQueued;
    private bool transcriptChanged = true;

    public MessageLogView(RichTextBox transcriptControl)
    {
        log = transcriptControl;
        Name = "messageLogView";
        Margin = Padding.Empty;
        split = new SplitContainer
        {
            Name = "messageLogSplit",
            Size = new Size(800, 500),
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Panel2Collapsed = true,
            SplitterWidth = 5,
            TabIndex = 0
        };
        gutter = new Panel { Name = "messageLogGutter", Dock = DockStyle.Left, Width = 28, TabIndex = 0 };
        split.Panel1.Controls.Add(log);
        split.Panel1.Controls.Add(gutter);
        log.TabIndex = 1;

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8, 5, 6, 5)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        boldFont = new Font(Font, FontStyle.Bold);
        heading = new Label
        {
            Name = "messageDetailsHeading",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = boldFont,
            Margin = new Padding(0, 0, 8, 0)
        };
        var collapse = new Button
        {
            Name = "collapseMessageButton",
            Text = "− Collapse",
            AutoSize = true,
            MinimumSize = new Size(90, 28),
            Margin = Padding.Empty,
            AccessibleName = "Collapse message field table"
        };
        collapse.Click += (_, _) => { Collapse(); log.Focus(); };
        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(collapse, 1, 0);

        grid = new DataGridView
        {
            Name = "messageFieldsGrid",
            AccessibleName = "Expanded SIP message fields",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
            EnableHeadersVisualStyles = false,
            TabIndex = 1
        };
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "fieldColumn", HeaderText = "Field", FillWeight = 28, MinimumWidth = 120, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "descriptionColumn", HeaderText = "Description", FillWeight = 41, MinimumWidth = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "valueColumn", HeaderText = "Value", FillWeight = 31, MinimumWidth = 140, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Collapse();
                log.Focus();
                e.Handled = true;
            }
        };
        split.Panel2.Controls.Add(grid);
        split.Panel2.Controls.Add(header);
        Controls.Add(split);

        log.TextChanged += TranscriptChanged;
        log.VScroll += QueueRefresh;
        log.HScroll += QueueRefresh;
        log.Resize += QueueRefresh;
        log.FontChanged += QueueRefresh;
        log.HandleCreated += QueueRefresh;
        RefreshTheme();
    }

    public void RefreshTheme()
    {
        BackColor = split.BackColor = AppTheme.Border;
        gutter.BackColor = AppTheme.LogBackground;
        heading.Parent.BackColor = heading.BackColor = AppTheme.Surface;
        UpdateGrid(() =>
        {
            grid.BackgroundColor = grid.DefaultCellStyle.BackColor = AppTheme.LogBackground;
            grid.DefaultCellStyle.ForeColor = AppTheme.Text;
            grid.GridColor = AppTheme.Border;
            grid.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.Surface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.Text;
            grid.ColumnHeadersDefaultCellStyle.Font = boldFont;
            grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
            foreach (DataGridViewRow row in grid.Rows)
            {
                StyleRow(row);
            }
        });
        foreach (var button in buttons.Values)
        {
            StyleButton(button);
        }
    }

    private void TranscriptChanged(object sender, EventArgs e)
    {
        transcriptChanged = true;
        QueueRefresh(sender, e);
    }

    private void QueueRefresh(object sender, EventArgs e)
    {
        if (IsDisposed || Disposing || refreshQueued)
        {
            return;
        }
        if (!IsHandleCreated)
        {
            return; // OnHandleCreated reads any text supplied before the form opens.
        }
        refreshQueued = true;
        BeginInvoke(() =>
        {
            refreshQueued = false;
            if (!IsDisposed && !Disposing)
            {
                RefreshMessages();
            }
        });
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        QueueRefresh(this, e);
    }

    private void RefreshMessages()
    {
        var text = transcriptChanged ? log.Text : transcript;
        transcriptChanged = false;
        if (text != transcript)
        {
            var expanded = messages.FirstOrDefault(message => message.Offset == expandedOffset);
            transcript = text;
            messages.Clear();
            for (var start = 0; start < text.Length;)
            {
                var end = text.IndexOf('\n', start);
                if (end < 0)
                {
                    end = text.Length;
                }
                var line = text.AsSpan(start, end - start).TrimEnd('\r');
                if (line.StartsWith("--> ", StringComparison.Ordinal) || line.StartsWith("<-- ", StringComparison.Ordinal))
                {
                    messages.Add(new(start, line[0] == '-', line[4..].ToString(), messages.Count + 1));
                }
                start = end + 1;
            }
            if (expanded != null && !messages.Contains(expanded))
            {
                Collapse();
            }
        }
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        if (!log.IsHandleCreated)
        {
            return;
        }
        gutter.SuspendLayout();
        try
        {
            var visible = new HashSet<int>();
            var height = Math.Max(16, log.Font.Height);
            var width = Math.Max(20, height);
            gutter.Width = width + 8;
            var firstVisible = log.GetCharIndexFromPosition(Point.Empty);
            var first = messages.FindIndex(message => message.Offset >= firstVisible);
            for (var index = Math.Max(0, first); first >= 0 && index < messages.Count; index++)
            {
                var message = messages[index];
                var point = log.GetPositionFromCharIndex(message.Offset);
                if (point.Y >= log.ClientSize.Height)
                {
                    break;
                }
                if (point.Y < 0)
                {
                    continue;
                }
                visible.Add(message.Offset);
                if (!buttons.TryGetValue(message.Offset, out var button))
                {
                    button = new ExpandButton { Name = $"expandMessage{message.Offset}Button", Tag = message.Offset, TabIndex = message.Number - 1 };
                    button.Click += (_, _) => ToggleMessage((int)button.Tag);
                    gutter.Controls.Add(button);
                    buttons.Add(message.Offset, button);
                }
                var expanded = message.Offset == expandedOffset;
                button.Text = expanded ? "−" : "+";
                button.AccessibleName = $"{(expanded ? "Collapse" : "Expand")} {(message.Sent ? "sent" : "received")} message {message.Number}";
                button.AccessibleDescription = "Show each field and its value, including individual flags, in the table below the transcript.";
                button.SetBounds(3, point.Y, width, height);
                StyleButton(button);
            }
            foreach (var offset in buttons.Keys.Where(offset => !visible.Contains(offset)).ToArray())
            {
                buttons[offset].Dispose();
                buttons.Remove(offset);
            }
        }
        finally { gutter.ResumeLayout(); }
    }

    private static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = AppTheme.Border;
        button.BackColor = AppTheme.LogBackground;
        button.ForeColor = AppTheme.Text;
        button.Padding = Padding.Empty;
        button.Margin = Padding.Empty;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.MouseOverBackColor = AppTheme.Input;
    }

    private void ToggleMessage(int offset)
    {
        if (expandedOffset == offset)
        {
            Collapse();
            return;
        }
        var message = messages.FirstOrDefault(message => message.Offset == offset);
        if (message == null)
        {
            return;
        }
        var details = SipLogFields.DescribeMessage(message.Text);
        heading.Text = $"{(message.Sent ? "Sent" : "Received")} message {message.Number} · {details.Name}";
        UpdateGrid(() =>
        {
            grid.Rows.Clear();
            var rows = new List<DataGridViewRow>();
            foreach (var field in details.Fields)
            {
                rows.Add(CreateRow(field));
                if (field.Children != null)
                {
                    foreach (var flag in field.Children)
                    {
                        rows.Add(CreateRow(flag, child: true));
                    }
                }
            }
            grid.Rows.AddRange(rows.ToArray());
            var opening = split.Panel2Collapsed;
            expandedOffset = offset;
            split.Panel2Collapsed = false;
            if (opening && split.Height > 140)
            {
                split.SplitterDistance = Math.Max(70, split.Height / 3);
            }
        });
        grid.CurrentCell = null;
        grid.ClearSelection();
        RefreshButtons();
    }

    private void UpdateGrid(Action update)
    {
        var rowSizing = grid.AutoSizeRowsMode;
        var columnSizing = grid.AutoSizeColumnsMode;
        grid.SuspendLayout();
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        try
        {
            update();
        }
        finally
        {
            // SuspendLayout alone does not stop DataGridView's cell measuring.
            // Fit columns first, then measure wrapped rows at their final widths.
            grid.ResumeLayout();
            grid.AutoSizeColumnsMode = columnSizing;
            grid.AutoSizeRowsMode = rowSizing;
        }
    }

    private DataGridViewRow CreateRow(SipLogDetail field, bool child = false)
    {
        var row = new DataGridViewRow();
        row.CreateCells(grid, field.Name, field.Description, child ? field.Meaning : field.DisplayValue);
        row.Tag = field;
        if (child)
        {
            row.Cells[0].Style.Padding = new Padding(20, 4, 6, 4);
            row.Cells[0].Value = "↳ " + field.Name;
        }
        StyleRow(row);
        return row;
    }

    private void StyleRow(DataGridViewRow row)
    {
        var detail = (SipLogDetail)row.Tag;
        row.DefaultCellStyle.BackColor = detail.Children != null ? AppTheme.Surface : AppTheme.LogBackground;
        row.DefaultCellStyle.ForeColor = AppTheme.Text;
        row.DefaultCellStyle.Font = detail.Children != null ? boldFont : grid.Font;
        row.Cells[2].Style.Font = detail.State == SipLogValueState.Normal ? row.DefaultCellStyle.Font : boldFont;
        row.Cells[2].Style.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : detail.State switch
        {
            SipLogValueState.Set => AppTheme.IsDarkMode ? Color.FromArgb(146, 214, 166) : Color.FromArgb(24, 104, 52),
            SipLogValueState.Invalid => AppTheme.IsDarkMode ? Color.FromArgb(255, 198, 109) : Color.FromArgb(144, 73, 0),
            _ => AppTheme.Text
        };
    }

    private void Collapse()
    {
        expandedOffset = -1;
        split.Panel2Collapsed = true;
        grid.Rows.Clear();
        RefreshButtons();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            log.TextChanged -= TranscriptChanged;
            log.VScroll -= QueueRefresh;
            log.HScroll -= QueueRefresh;
            log.Resize -= QueueRefresh;
            log.FontChanged -= QueueRefresh;
            log.HandleCreated -= QueueRefresh;
        }
        base.Dispose(disposing);
        if (disposing)
        {
            boldFont.Dispose();
        }
    }

    private sealed class ExpandButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            // A normal button's text padding can hide a glyph at the height of a
            // transcript line. Draw the sign directly while keeping button input
            // and accessibility behavior.
            var hovered = ClientRectangle.Contains(PointToClient(MousePosition));
            e.Graphics.Clear(hovered || Text == "−" ? AppTheme.Input : BackColor);
            using var border = new Pen(Focused ? SystemColors.Highlight : AppTheme.Border);
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            using var sign = new Pen(ForeColor);
            var center = new Point(Width / 2, Height / 2);
            var radius = Math.Max(3, Height / 4);
            e.Graphics.DrawLine(sign, center.X - radius, center.Y, center.X + radius, center.Y);
            if (Text == "+")
            {
                e.Graphics.DrawLine(sign, center.X, center.Y - radius, center.X, center.Y + radius);
            }
            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), ForeColor, BackColor);
            }
        }
    }
}
