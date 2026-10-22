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
            ? (dark ? Brushes.Salmon : Brushes.Crimson)
            : text.StartsWith("<-- ", StringComparison.Ordinal) ? (dark ? Brushes.SkyBlue : Brushes.RoyalBlue) : null;
        if (brush != null)
        {
            ChangeLinePart(line.Offset, line.EndOffset, part => part.TextRunProperties.SetForegroundBrush(brush));
            var separator = new SolidColorBrush(dark ? Color.Parse("#9AA0A8") : Color.Parse("#777777"));
            for (var i = 4; i < text.Length; i++)
            {
                if (text[i] == '|') { ChangeLinePart(line.Offset + i, line.Offset + i + 1, part => part.TextRunProperties.SetForegroundBrush(separator)); }
            }
        }
    }
}
