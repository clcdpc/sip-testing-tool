namespace Clc.Sip.Helpers;

public class ThemedButton : Button
{
    protected override void OnPaint(PaintEventArgs args)
    {
        if (Enabled || !AppTheme.IsDarkMode || SystemInformation.HighContrast)
        {
            base.OnPaint(args);
            return;
        }

        // Win32's disabled text can remain black after a live color-mode change.
        using var background = new SolidBrush(Color.FromArgb(43, 47, 54));
        args.Graphics.FillRectangle(background, ClientRectangle);
        using var border = new Pen(AppTheme.Border);
        args.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(args.Graphics, Text, Font, ClientRectangle, Color.FromArgb(151, 160, 175),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.HidePrefix);
    }
}
