using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Clc.Sip.Helpers;

public sealed class MessageLogToolTips : Component
{
    private readonly RichTextBox log;
    private readonly Form owner;
    private readonly ToolTip toolTip = new() { ShowAlways = true };
    private readonly System.Windows.Forms.Timer hoverTimer = new() { Interval = 450 };
    private readonly OutlineWindow outline;
    private string transcript;
    private SipLogField hoveredField;
    private SipLogField pinnedField;
    private SipLogField pendingClick;
    private Point hoverPoint;
    private bool disposed;

    public SipLogField OutlinedField => hoveredField?.IsFixed == true ? hoveredField : pinnedField;

    public MessageLogToolTips(RichTextBox log, IContainer components)
    {
        this.log = log;
        owner = log.FindForm();
        outline = new OutlineWindow(this);
        AttachOutline(this, EventArgs.Empty);
        components.Add(this);
        log.AccessibleDescription = "Hover over SIP field codes or fixed values for help. Status and summary flags have individual help and a set/not-set checklist. Click a field or flag to keep its outline; press Escape to clear it. Select text to copy raw messages.";
        log.MouseMove += MouseMoved;
        log.MouseLeave += DismissHover;
        log.MouseDown += MouseDown;
        log.MouseUp += MouseUp;
        log.MouseWheel += DismissHover;
        log.KeyDown += KeyDown;
        log.VScroll += DismissHover;
        log.HScroll += DismissHover;
        log.Resize += DismissHover;
        log.FontChanged += DismissHover;
        log.BackColorChanged += DismissHover;
        log.VisibleChanged += DismissHover;
        log.HandleCreated += AttachOutline;
        log.HandleDestroyed += DetachOutline;
        log.TextChanged += TextChanged;
        hoverTimer.Tick += Show;
        if (owner != null)
        {
            owner.Deactivate += DismissHover;
        }
    }

    public SipLogField GetFieldAt(Point position)
    {
        if (!log.ClientRectangle.Contains(position) || log.TextLength == 0)
        {
            return null;
        }

        transcript ??= log.Text;
        var index = log.GetCharIndexFromPosition(position);
        var field = SipLogFields.Find(transcript, index);
        if (field == null)
        {
            return null;
        }

        // RichTextBox returns the *nearest* character even over empty space.
        // Check the actual glyph area, including identifiers split by wrapping.
        return CharacterBounds(index).Contains(position) ? field : null;
    }

    private Rectangle CharacterBounds(int index)
    {
        var start = log.GetPositionFromCharIndex(index);
        var next = log.GetPositionFromCharIndex(index + 1);
        var width = next.Y == start.Y && next.X > start.X ? next.X - start.X
            : (int)Math.Ceiling(TextRenderer.MeasureText(transcript[index].ToString(), log.Font, Size.Empty, TextFormatFlags.NoPadding).Width * log.ZoomFactor);
        var height = (int)Math.Ceiling(log.Font.Height * log.ZoomFactor);
        return new Rectangle(start, new Size(width, height));
    }

    public IReadOnlyList<Rectangle> GetOutlineBounds(SipLogField field)
    {
        if (field?.IsFixed != true || field.Offset < 0 || field.Offset + field.Length > log.TextLength)
        {
            return Array.Empty<Rectangle>();
        }

        transcript ??= log.Text;
        var rows = new List<Rectangle>();
        for (var index = field.Offset; index < field.Offset + field.Length; index++)
        {
            var bounds = CharacterBounds(index);
            if (rows.Count > 0 && rows[^1].Y == bounds.Y)
            {
                rows[^1] = Rectangle.Union(rows[^1], bounds);
            }
            else
            {
                rows.Add(bounds);
            }
        }
        return rows.Where(row => row.IntersectsWith(log.ClientRectangle)).ToArray();
    }

    private void MouseMoved(object sender, MouseEventArgs args)
    {
        var field = args.Button == MouseButtons.None ? GetFieldAt(args.Location) : null;
        if (field == hoveredField)
        {
            return;
        }

        DismissHover(sender, args);
        hoveredField = field;
        if (field != null)
        {
            hoverPoint = args.Location;
            hoverTimer.Start();
            log.Invalidate();
        }
    }

    private void Show(object sender, EventArgs args)
    {
        hoverTimer.Stop();
        if (hoveredField != null && log.Visible)
        {
            ShowField(hoveredField, hoverPoint);
        }
    }

    private void ShowField(SipLogField field, Point position)
    {
        var lineHeight = (int)Math.Ceiling(log.Font.Height * log.ZoomFactor);
        toolTip.Show(field.Description, log, position.X + 12, position.Y + lineHeight + 4, 15000);
    }

    private void DismissHover(object sender, EventArgs args)
    {
        hoverTimer.Stop();
        hoveredField = null;
        if (log.IsHandleCreated && !log.IsDisposed)
        {
            toolTip.Hide(log);
            log.Invalidate();
        }
    }

    private void MouseDown(object sender, MouseEventArgs args)
    {
        DismissHover(sender, args);
        if (args.Button == MouseButtons.Left)
        {
            pinnedField = null;
            pendingClick = args.Clicks == 1 ? GetFieldAt(args.Location) : null;
        }
    }

    private void MouseUp(object sender, MouseEventArgs args)
    {
        var clicked = pendingClick;
        pendingClick = null;
        if (args.Button == MouseButtons.Left && log.SelectionLength == 0 && clicked?.IsFixed == true && clicked == GetFieldAt(args.Location))
        {
            pinnedField = clicked;
            ShowField(clicked, args.Location);
            log.Invalidate();
        }
    }

    private void KeyDown(object sender, KeyEventArgs args)
    {
        pinnedField = pendingClick = null;
        DismissHover(sender, args);
    }

    private void TextChanged(object sender, EventArgs args)
    {
        // RichEdit also raises TextChanged for formatting. A theme change must
        // keep a clicked field when its character range still refers to the same text.
        if (transcript != null && (hoveredField != null || pinnedField != null || pendingClick != null) &&
            string.Equals(transcript, log.Text, StringComparison.Ordinal))
        {
            return;
        }
        transcript = null;
        pinnedField = pendingClick = null;
        DismissHover(sender, args);
    }

    private void AttachOutline(object sender, EventArgs args)
    {
        if (log.IsHandleCreated)
        {
            outline.AssignHandle(log.Handle);
        }
    }

    private void DetachOutline(object sender, EventArgs args) => outline.ReleaseHandle();

    private void DrawOutline(Graphics graphics)
    {
        var field = OutlinedField;
        if (field == null)
        {
            return;
        }

        graphics.SetClip(log.ClientRectangle);
        if (field.FlagGroup != null)
        {
            DrawFlagGroup(graphics, field.FlagGroup);
        }
        var color = SystemInformation.HighContrast ? SystemColors.Highlight : AppTheme.IsDarkMode ? Color.FromArgb(137, 156, 178) : Color.FromArgb(112, 132, 156);
        using var pen = new Pen(color, Math.Max(1, log.DeviceDpi / 96f)) { DashStyle = field == pinnedField ? DashStyle.Solid : DashStyle.Dot, Alignment = PenAlignment.Inset };
        foreach (var row in GetOutlineBounds(field))
        {
            var bounds = Rectangle.Inflate(row, 1, 0);
            graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }
    }

    private void DrawFlagGroup(Graphics graphics, SipLogFlagGroup group)
    {
        var gridColor = SystemInformation.HighContrast ? SystemColors.GrayText : AppTheme.IsDarkMode ? Color.FromArgb(77, 91, 108) : Color.FromArgb(202, 210, 220);
        var setColor = SystemInformation.HighContrast ? SystemColors.Highlight : AppTheme.IsDarkMode ? Color.FromArgb(141, 183, 225) : Color.FromArgb(57, 105, 158);
        var invalidColor = SystemInformation.HighContrast ? SystemColors.WindowText : AppTheme.IsDarkMode ? Color.FromArgb(231, 183, 110) : Color.FromArgb(168, 100, 23);
        using var grid = new Pen(gridColor);
        using var active = new SolidBrush(setColor);
        using var invalid = new SolidBrush(invalidColor);
        transcript ??= log.Text;
        for (var index = 0; index < group.Value.Length; index++)
        {
            var bounds = CharacterBounds(group.Offset + index);
            if (!bounds.IntersectsWith(log.ClientRectangle))
            {
                continue;
            }
            graphics.DrawRectangle(grid, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            if (group.IsSet(index) || !group.IsValid(index))
            {
                graphics.FillRectangle(group.IsSet(index) ? active : invalid, bounds.X + 1, bounds.Bottom - 3, Math.Max(1, bounds.Width - 2), 2);
            }
        }
    }

    private sealed class OutlineWindow(MessageLogToolTips help) : NativeWindow
    {
        protected override void WndProc(ref Message message)
        {
            var paint = message.Msg is 0x000F or 0x0317 or 0x0318; // WM_PAINT, WM_PRINT, WM_PRINTCLIENT
            var printDc = message.Msg is 0x0317 or 0x0318 ? message.WParam : IntPtr.Zero;
            base.WndProc(ref message);
            if (!paint || help.disposed || help.OutlinedField == null || !help.log.IsHandleCreated)
            {
                return;
            }

            // Paint above RichEdit's native text without changing its RTF,
            // selection, clipboard content, or keyboard/mouse handling.
            using var graphics = printDc == IntPtr.Zero ? Graphics.FromHwnd(help.log.Handle) : Graphics.FromHdc(printDc);
            help.DrawOutline(graphics);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            pinnedField = pendingClick = null;
            DismissHover(this, EventArgs.Empty);
            outline.ReleaseHandle();
            log.MouseMove -= MouseMoved;
            log.MouseLeave -= DismissHover;
            log.MouseDown -= MouseDown;
            log.MouseUp -= MouseUp;
            log.MouseWheel -= DismissHover;
            log.KeyDown -= KeyDown;
            log.VScroll -= DismissHover;
            log.HScroll -= DismissHover;
            log.Resize -= DismissHover;
            log.FontChanged -= DismissHover;
            log.BackColorChanged -= DismissHover;
            log.VisibleChanged -= DismissHover;
            log.HandleCreated -= AttachOutline;
            log.HandleDestroyed -= DetachOutline;
            log.TextChanged -= TextChanged;
            if (owner != null)
            {
                owner.Deactivate -= DismissHover;
            }
            hoverTimer.Dispose();
            toolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
