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
        }
    }
}
