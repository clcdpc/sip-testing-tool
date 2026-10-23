using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Clc.Sip.Desktop.Views;

public sealed class OptionalFieldRow : Panel
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<OptionalFieldRow, string>(nameof(Label), "");
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    private readonly TextBlock caption = new() { Classes = { "fieldLabel" }, Margin = new Thickness(0), TextWrapping = TextWrapping.Wrap };
    private Control? input;
    private double labelWidth;
    private double minimumFieldWidth = 120;

    [Content]
    public Control? Field
    {
        get => input;
        set
        {
            if (input == value) { return; }
            if (input != null) { Children.Remove(input); }
            input = value;
            if (input != null) { Children.Add(input); }
            InvalidateMeasure();
        }
    }

    public OptionalFieldRow() => Children.Add(caption);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty) { caption.Text = Label; }
    }

    private double MinimumFieldWidth => minimumFieldWidth;
    private bool IsInline(double width)
    {
        var captionWidth = input is ComboBox ? Math.Max(labelWidth, Math.Min(140, width - 8 - 140)) : labelWidth;
        return captionWidth + 8 + MinimumFieldWidth <= width;
    }
    private double LabelColumnWidth(double width) => Math.Max(labelWidth, Math.Min(140, width - 8 - MinimumFieldWidth));

    protected override Size MeasureOverride(Size availableSize)
    {
        caption.Measure(Size.Infinity);
        labelWidth = caption.DesiredSize.Width;
        if (input == null) { return caption.DesiredSize; }
        // Reserve enough space for the selected choice before deciding whether
        // its label can stay beside it. A constrained measure can hide truncation.
        if (input is ComboBox) { input.Measure(Size.Infinity); }
        minimumFieldWidth = input is OptionalDateTimeInput ? 230 : input is ComboBox ? Math.Max(140, input.DesiredSize.Width) : 120;
        var width = double.IsInfinity(availableSize.Width) ? labelWidth + 8 + MinimumFieldWidth : availableSize.Width;
        if (IsInline(width))
        {
            var column = LabelColumnWidth(width);
            caption.Measure(new Size(column, double.PositiveInfinity));
            input.Measure(new Size(Math.Max(0, width - column - 8), double.PositiveInfinity));
            return new Size(column + 8 + input.DesiredSize.Width, Math.Max(caption.DesiredSize.Height, input.DesiredSize.Height));
        }
        caption.Measure(new Size(width, double.PositiveInfinity));
        input.Measure(new Size(width, double.PositiveInfinity));
        return new Size(Math.Max(caption.DesiredSize.Width, input.DesiredSize.Width), caption.DesiredSize.Height + 4 + input.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (input == null) { caption.Arrange(new Rect(finalSize)); return finalSize; }
        if (IsInline(finalSize.Width))
        {
            var column = LabelColumnWidth(finalSize.Width);
            caption.Arrange(new Rect(0, Math.Max(0, (finalSize.Height - caption.DesiredSize.Height) / 2), column, caption.DesiredSize.Height));
            input.Arrange(new Rect(column + 8, Math.Max(0, (finalSize.Height - input.DesiredSize.Height) / 2), Math.Max(0, finalSize.Width - column - 8), input.DesiredSize.Height));
        }
        else
        {
            caption.Arrange(new Rect(0, 0, finalSize.Width, caption.DesiredSize.Height));
            input.Arrange(new Rect(0, caption.DesiredSize.Height + 4, finalSize.Width, input.DesiredSize.Height));
        }
        return finalSize;
    }
}
