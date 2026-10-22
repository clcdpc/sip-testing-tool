using System.Runtime.InteropServices;

namespace Clc.Sip.Helpers
{
    public static class RichTextBoxExtensions
    {
        public static void AppendText(this RichTextBox box, string text, Color color)
        {
            var start = box.TextLength;
            box.Select(start, 0);
            box.SelectionColor = color;
            box.AppendText(text);
            ColorSeparators(box, text, start);
            box.Select(box.TextLength, 0);
            box.SelectionColor = box.ForeColor;
        }

        public static void RecolorTranscript(this RichTextBox box)
        {
            var selectionStart = box.SelectionStart;
            var selectionLength = box.SelectionLength;
            var scroll = new Point();
            var handle = box.IsHandleCreated ? box.Handle : IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                SendMessage(handle, 0x04DD, IntPtr.Zero, ref scroll); // EM_GETSCROLLPOS
                SendMessage(handle, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW
            }
            try
            {
                var offset = 0;
                foreach (var line in box.Lines)
                {
                    box.Select(offset, line.Length);
                    box.SelectionColor = line.StartsWith("--> ", StringComparison.Ordinal) ? AppTheme.Sent
                        : line.StartsWith("<-- ", StringComparison.Ordinal) ? AppTheme.Received : AppTheme.Text;
                    ColorSeparators(box, line, offset);
                    offset += line.Length + 1;
                }
            }
            finally
            {
                box.Select(selectionStart, selectionLength);
                if (selectionLength == 0)
                {
                    box.SelectionColor = box.ForeColor;
                }

                if (handle != IntPtr.Zero)
                {
                    SendMessage(handle, 0x04DE, IntPtr.Zero, ref scroll); // EM_SETSCROLLPOS
                    SendMessage(handle, 0x000B, new IntPtr(1), IntPtr.Zero);
                    box.Invalidate();
                }
            }
        }

        private static void ColorSeparators(RichTextBox box, string text, int offset)
        {
            for (var index = text.IndexOf('|'); index >= 0; index = text.IndexOf('|', index + 1))
            {
                box.Select(offset + index, 1);
                box.SelectionColor = AppTheme.Separator;
            }
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, ref Point point);
    }
}
