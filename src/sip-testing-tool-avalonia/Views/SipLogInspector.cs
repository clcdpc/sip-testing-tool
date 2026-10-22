using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Clc.Sip.Helpers;

namespace Clc.Sip.Desktop.Views;

public sealed class SipLogInspector : IDisposable
{
    private readonly TextEditor editor;
    private readonly MessageFieldsPanel panel;
    private readonly MessageMargin margin;
    private readonly InspectionLayer overlay;
    private TextDocument? document;
    private SipLogField? hover;
    private SipLogField? pinned;
    private DocumentLine? expandedLine;
    private string? expandedText;
    private int cachedLineNumber;
    private string? cachedLine;
    public SipLogField? HoveredField => hover;
    public SipLogField? PinnedField => pinned;
    public event Action<bool>? ExpansionChanged;

    public SipLogInspector(TextEditor editor, MessageFieldsPanel panel)
    {
        this.editor = editor;
        this.panel = panel;
        margin = new MessageMargin(this);
        overlay = new InspectionLayer(this) { IsHitTestVisible = false, Focusable = false };
        editor.TextArea.LeftMargins.Insert(0, margin);
        editor.TextArea.TextView.InsertLayer(overlay, KnownLayer.Text, LayerInsertionPosition.Above);
        editor.TextArea.TextView.VisualLinesChanged += GeometryChanged;
        editor.TextArea.TextView.ScrollOffsetChanged += Scrolled;
        editor.TextArea.TextView.PointerMoved += PointerMoved;
        editor.TextArea.TextView.PointerExited += PointerExited;
        editor.TextArea.TextView.PointerPressed += PointerPressed;
        editor.TextArea.SelectionChanged += SelectionChanged;
        editor.AddHandler(InputElement.KeyDownEvent, EditorKeyDown, RoutingStrategies.Tunnel);
        editor.DocumentChanged += DocumentChanged;
        panel.CollapseRequested += Collapse;
        DocumentChanged(this, EventArgs.Empty);
    }

    private void DocumentChanged(object? sender, EventArgs args)
    {
        if (document != null) { document.TextChanged -= TextChanged; }
        document = editor.Document;
        if (document != null) { document.TextChanged += TextChanged; }
        Collapse();
        TextChanged(sender, args);
    }

    private void TextChanged(object? sender, EventArgs args)
    {
        cachedLine = null;
        if (document?.TextLength == 0 || expandedLine?.IsDeleted == true) { Collapse(); }
        if (pinned != null && pinned.Offset + pinned.Length > (document?.TextLength ?? 0)) { pinned = null; }
        SetHover(null);
        margin.InvalidateVisual();
    }

    public void ExpandMessageAtOffset(int offset)
    {
        if (document == null || offset < 0 || offset > document.TextLength) { return; }
        var line = document.GetLineByOffset(offset);
        var text = document.GetText(line);
        if (!IsMessage(text)) { return; }
        if (expandedLine == line && expandedText == text) { Collapse(); return; }
        expandedLine = line;
        expandedText = text;
        panel.ShowMessage(SipLogFields.DescribeMessage(text[4..]));
        ExpansionChanged?.Invoke(true);
        margin.InvalidateVisual();
    }

    public void Collapse()
    {
        expandedLine = null;
        expandedText = null;
        ExpansionChanged?.Invoke(false);
        margin.InvalidateVisual();
    }

    public void InspectOffset(int offset, bool pin = false)
    {
        SipLogField? field = null;
        if (document != null && offset >= 0 && offset < document.TextLength)
        {
            var line = document.GetLineByOffset(offset);
            if (cachedLine == null || cachedLineNumber != line.LineNumber)
            {
                cachedLineNumber = line.LineNumber;
                cachedLine = document.GetText(line);
            }
            var relative = SipLogFields.Find(cachedLine, offset - line.Offset);
            if (relative != null)
            {
                field = relative with
                {
                    Offset = relative.Offset + line.Offset,
                    FlagGroup = relative.FlagGroup == null ? null : relative.FlagGroup with { Offset = relative.FlagGroup.Offset + line.Offset }
                };
            }
        }
        if (pin)
        {
            pinned = field?.IsFixed == true ? field : null;
            RefreshTheme();
        }
        SetHover(field);
    }

    private void SetHover(SipLogField? field)
    {
        if (hover == field) { return; }
        hover = field;
        ToolTip.SetIsOpen(editor, false);
        if (field != null)
        {
            ToolTip.SetTip(editor, new TextBlock { Text = field.Description, MaxWidth = 520, TextWrapping = TextWrapping.Wrap });
            ToolTip.SetPlacement(editor, PlacementMode.Pointer);
            ToolTip.SetIsOpen(editor, true);
        }
        overlay.InvalidateVisual();
    }

    private int? PointerOffset(PointerEventArgs args)
    {
        var position = editor.GetPositionFromPoint(args.GetPosition(editor));
        return position.HasValue && document != null ? document.GetOffset(position.Value.Location) : null;
    }

    private void PointerMoved(object? sender, PointerEventArgs args)
    {
        if (!editor.TextArea.Selection.IsEmpty && args.GetCurrentPoint(editor).Properties.IsLeftButtonPressed)
        {
            pinned = null;
            SetHover(null);
            return;
        }
        InspectOffset(PointerOffset(args) ?? -1);
    }

    private void PointerExited(object? sender, PointerEventArgs args) => SetHover(null);

    private void PointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (args.GetCurrentPoint(editor).Properties.IsLeftButtonPressed) { InspectOffset(PointerOffset(args) ?? -1, true); }
    }

    private void SelectionChanged(object? sender, EventArgs args)
    {
        if (!editor.TextArea.Selection.IsEmpty) { pinned = null; SetHover(null); RefreshTheme(); }
    }

    private void EditorKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape) { pinned = null; SetHover(null); RefreshTheme(); }
        if (args.Key == Key.Enter && args.KeyModifiers == KeyModifiers.Control)
        {
            ExpandMessageAtOffset(editor.CaretOffset);
            args.Handled = true;
        }
    }

    public void RefreshTheme()
    {
        overlay.InvalidateVisual();
        margin.InvalidateVisual();
    }

    private void GeometryChanged(object? sender, EventArgs args) => overlay.InvalidateVisual();

    private void Scrolled(object? sender, EventArgs args)
    {
        SetHover(null);
        RefreshTheme();
    }

    public void Draw(TextView textView, DrawingContext context)
    {
        if (!textView.VisualLinesValid) { return; }
        var field = pinned ?? (hover?.IsFixed == true ? hover : null);
        if (field == null || field.Offset + field.Length > (document?.TextLength ?? 0)) { return; }
        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var outline = new Pen(dark ? Brushes.LightSteelBlue : Brushes.SteelBlue, 1,
            pinned == null ? new DashStyle([2, 2], 0) : null);
        if (field.FlagGroup is { } group)
        {
            var cellPen = new Pen(dark ? new SolidColorBrush(Color.FromArgb(75, 160, 180, 200)) : new SolidColorBrush(Color.FromArgb(65, 60, 85, 110)), 1);
            for (var i = 0; i < group.Value.Length; i++)
            {
                foreach (var rect in Rects(textView, group.Offset + i, 1))
                {
                    context.DrawRectangle(null, cellPen, rect);
                    if (group.IsSet(i) || !group.IsValid(i))
                    {
                        var underline = group.IsSet(i) ? outline : new Pen(dark ? Brushes.SandyBrown : Brushes.DarkGoldenrod, 2);
                        context.DrawLine(underline, rect.BottomLeft, rect.BottomRight);
                    }
                }
            }
        }
        foreach (var rect in Rects(textView, field.Offset, field.Length)) { context.DrawRectangle(null, outline, rect.Inflate(new Thickness(1, 0))); }
    }

    private static IEnumerable<Rect> Rects(TextView view, int offset, int length) =>
        BackgroundGeometryBuilder.GetRectsForSegment(view, new SimpleSegment(offset, length), false);

    private static bool IsMessage(string text) => text.StartsWith("--> ", StringComparison.Ordinal) || text.StartsWith("<-- ", StringComparison.Ordinal);

    public void Dispose()
    {
        if (document != null) { document.TextChanged -= TextChanged; }
        editor.DocumentChanged -= DocumentChanged;
        editor.TextArea.TextView.PointerMoved -= PointerMoved;
        editor.TextArea.TextView.PointerExited -= PointerExited;
        editor.TextArea.TextView.PointerPressed -= PointerPressed;
        editor.TextArea.SelectionChanged -= SelectionChanged;
        editor.RemoveHandler(InputElement.KeyDownEvent, EditorKeyDown);
        panel.CollapseRequested -= Collapse;
        editor.TextArea.TextView.VisualLinesChanged -= GeometryChanged;
        editor.TextArea.TextView.ScrollOffsetChanged -= Scrolled;
        editor.TextArea.TextView.Layers.Remove(overlay);
        editor.TextArea.LeftMargins.Remove(margin);
        ToolTip.SetIsOpen(editor, false);
    }

    // Keep outlines in their own layer so moving the pointer never rewrites or
    // invalidates formatted text, and does not depend on the caret blinking.
    private sealed class InspectionLayer(SipLogInspector owner) : Control
    {
        public override void Render(DrawingContext context) => owner.Draw(owner.editor.TextArea.TextView, context);
    }

    private sealed class MessageMargin(SipLogInspector owner) : AbstractMargin
    {
        protected override Size MeasureOverride(Size availableSize) => new(25, 0);
        protected override void OnTextViewVisualLinesChanged() { base.OnTextViewVisualLinesChanged(); InvalidateVisual(); }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (TextView?.VisualLinesValid != true || owner.document == null) { return; }
            var pen = new Pen(Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? Brushes.LightSteelBlue : Brushes.SteelBlue, 1);
            foreach (var visual in TextView.VisualLines)
            {
                var line = visual.FirstDocumentLine;
                if (!IsMessage(owner.document.GetText(line))) { continue; }
                var y = visual.VisualTop - TextView.VerticalOffset + 3;
                var rect = new Rect(5, y, 13, 13);
                context.DrawRectangle(null, pen, rect, 2, 2);
                context.DrawLine(pen, new Point(8, y + 6.5), new Point(15, y + 6.5));
                if (owner.expandedLine != line) { context.DrawLine(pen, new Point(11.5, y + 3), new Point(11.5, y + 10)); }
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs args)
        {
            base.OnPointerPressed(args);
            if (TextView?.VisualLinesValid != true || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { return; }
            var y = args.GetPosition(this).Y + TextView.VerticalOffset;
            var line = TextView.VisualLines.FirstOrDefault(visual => y >= visual.VisualTop && y < visual.VisualTop + Math.Min(visual.Height, 22));
            if (line == null) { return; }
            owner.ExpandMessageAtOffset(line.FirstDocumentLine.Offset);
            args.Handled = true;
        }
    }
}
