namespace Clc.Sip.Helpers;

public class ThemedComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmPrint = 0x0317;
    private const int WmPrintClient = 0x0318;

    protected override void WndProc(ref Message message)
    {
        var paint = message.Msg is WmPaint or WmPrint or WmPrintClient;
        // ComboBox's native paint handler may rewrite Message.WParam to a flag.
        // Preserve the print DC before handing the message to it.
        var printDc = message.Msg is WmPrint or WmPrintClient ? message.WParam : IntPtr.Zero;
        base.WndProc(ref message);
        if (!AppTheme.IsDarkMode || SystemInformation.HighContrast || DropDownStyle == ComboBoxStyle.Simple
            || !paint)
        {
            return;
        }

        // Native combo buttons retain their light face in classic process color mode.
        // Paint the chrome for both the live window and WM_PRINT previews; native
        // hit testing, editable text, keyboard navigation and accessibility stay intact.
        using var graphics = printDc == IntPtr.Zero ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(printDc);
        var button = new Rectangle(Width - LogicalToDeviceUnits(18), 1, LogicalToDeviceUnits(18) - 1, Height - 2);
        using var background = new SolidBrush(AppTheme.Surface);
        graphics.FillRectangle(background, button);
        ControlPaint.DrawBorder(graphics, ClientRectangle, AppTheme.Border, ButtonBorderStyle.Solid);
        using var inset = new Pen(AppTheme.Input);
        graphics.DrawRectangle(inset, 1, 1, button.Left - 2, Height - 3);
        var center = new Point(button.Left + button.Width / 2, button.Top + button.Height / 2);
        var radius = LogicalToDeviceUnits(3);
        using var arrow = new SolidBrush(Enabled ? AppTheme.Text : SystemColors.GrayText);
        graphics.FillPolygon(arrow, [new Point(center.X - radius, center.Y - 1), new Point(center.X + radius, center.Y - 1), new Point(center.X, center.Y + radius - 1)]);
    }

    protected override void OnDrawItem(DrawItemEventArgs args)
    {
        if (!AppTheme.IsDarkMode || SystemInformation.HighContrast)
        {
            base.OnDrawItem(args);
            return;
        }

        var edit = (args.State & DrawItemState.ComboBoxEdit) != 0;
        var selected = !edit && (args.State & DrawItemState.Selected) != 0;
        var background = selected ? Color.FromArgb(58, 76, 102) : AppTheme.Input;
        using var brush = new SolidBrush(background);
        args.Graphics.FillRectangle(brush, args.Bounds);
        var text = args.Index >= 0 && args.Index < Items.Count ? GetItemText(Items[args.Index]) : Text;
        var bounds = args.Bounds;
        bounds.Inflate(-2, 0);
        TextRenderer.DrawText(args.Graphics, text, args.Font ?? Font, bounds,
            Enabled ? AppTheme.Text : Color.FromArgb(151, 160, 175),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (!edit && (args.State & DrawItemState.Focus) != 0)
        {
            ControlPaint.DrawFocusRectangle(args.Graphics, args.Bounds, AppTheme.Text, background);
        }
    }
}
