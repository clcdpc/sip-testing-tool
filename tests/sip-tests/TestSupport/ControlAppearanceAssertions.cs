using Clc.Sip.Helpers;

namespace Clc.Sip.Tests.TestSupport;

internal static class ControlAppearanceAssertions
{
    public static void MatchCurrentTheme(TextBoxBase input)
    {
        using var image = new System.Drawing.Bitmap(input.Width, input.Height);
        input.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, input.Size));
        Assert.AreEqual(AppTheme.Input.ToArgb(), image.GetPixel(input.Width - 8, input.Height / 2).ToArgb());
        Assert.AreEqual(AppTheme.Text.ToArgb(), input.ForeColor.ToArgb());
        if (input is ThemedTextBox && AppTheme.IsDarkMode && !SystemInformation.HighContrast)
        {
            foreach (var x in new[] { 0, 1, input.Width / 2, input.Width - 2, input.Width - 1 })
            {
                foreach (var y in new[] { 0, 1, input.Height - 2, input.Height - 1 })
                {
                    var edge = image.GetPixel(x, y);
                    Assert.IsTrue(edge.R < 190 && edge.G < 190 && edge.B < 190, input.Name + " still has a bright native border.");
                }
            }
        }
    }
}
