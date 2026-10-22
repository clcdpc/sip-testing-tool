using System.Runtime.InteropServices;

namespace Clc.Sip.Helpers;

public class ThemedTextBox : TextBox
{
    private const int WmPaint = 0x000F;
    private const int WmNonClientPaint = 0x0085;
    private const int WmPrint = 0x0317;
    private const uint RedrawInvalidate = 0x0001;
    private const uint RedrawFrame = 0x0400;

    internal void RefreshTheme()
    {
        // Invalidate the non-client frame too, so switching back to light mode
        // restores the native border without recreating the edit control.
        if (IsHandleCreated)
        {
            _ = RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RedrawInvalidate | RedrawFrame);
        }
    }

    protected override void Select(bool directed, bool forward)
    {
        base.Select(directed, forward);
        // Directed selection is keyboard traversal; clicks retain native caret placement.
        if (directed)
        {
            SelectAll();
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        RefreshTheme();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        RefreshTheme();
    }

    protected override void WndProc(ref Message message)
    {
        var paint = message.Msg is WmPaint or WmNonClientPaint or WmPrint;
        var printDc = message.Msg == WmPrint ? message.WParam : IntPtr.Zero;
        base.WndProc(ref message);
        if (!paint || !AppTheme.IsDarkMode || SystemInformation.HighContrast || BorderStyle == BorderStyle.None || !IsHandleCreated)
        {
            return;
        }

        // Keep the standard Windows edit control, including selection, paste and
        // accessibility. Only replace its bright, beveled non-client border.
        var windowDc = printDc == IntPtr.Zero ? GetWindowDC(Handle) : IntPtr.Zero;
        var dc = printDc == IntPtr.Zero ? windowDc : printDc;
        if (dc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            using var graphics = Graphics.FromHdc(dc);
            using var border = new Pen(Focused ? Color.FromArgb(91, 118, 150) : AppTheme.Border);
            graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            if (BorderStyle == BorderStyle.Fixed3D)
            {
                using var inset = new Pen(BackColor);
                graphics.DrawRectangle(inset, 1, 1, Width - 3, Height - 3);
            }
        }
        finally
        {
            if (windowDc != IntPtr.Zero)
            {
                _ = ReleaseDC(Handle, windowDc);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr window);
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr window, IntPtr updateRectangle, IntPtr updateRegion, uint flags);
}
