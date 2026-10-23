using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Clc.Sip.Desktop.Views;

public sealed class SipLogColorizer : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        IBrush? brush = text.StartsWith("--> ", StringComparison.Ordinal)
            ? (dark ? new SolidColorBrush(Color.FromRgb(242, 139, 130)) : Brushes.Crimson)
            : text.StartsWith("<-- ", StringComparison.Ordinal) ? (dark ? new SolidColorBrush(Color.FromRgb(138, 180, 248)) : Brushes.RoyalBlue) : null;
        if (brush != null)
        {
            ChangeLinePart(line.Offset, line.EndOffset, part => part.TextRunProperties.SetForegroundBrush(brush));
            IBrush separator = dark ? new SolidColorBrush(Color.FromRgb(165, 174, 188)) : Brushes.Black;
            for (var i = 4; i < text.Length; i++)
            {
                if (text[i] == '|') { ChangeLinePart(line.Offset + i, line.Offset + i + 1, part => part.TextRunProperties.SetForegroundBrush(separator)); }
            }
        }
    }
}
