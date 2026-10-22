namespace Clc.Sip.Helpers;

internal sealed class OptionalFieldRow : Panel
{
    private readonly Label caption;
    private readonly Control input;
    private int measuredWidth;

    public OptionalFieldRow(Label caption, Control input)
    {
        this.caption = caption;
        this.input = input;
        Name = input.Name + "Row";
        TabIndex = input.TabIndex;
        TabStop = false;
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0, 0, 0, 12);
        caption.AutoSize = false;
        caption.TextAlign = ContentAlignment.MiddleLeft;
        caption.Dock = DockStyle.None;
        caption.Margin = Padding.Empty;
        input.Dock = DockStyle.None;
        input.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        input.Margin = Padding.Empty;
        input.TabIndex = 0;
        Controls.Add(caption);
        Controls.Add(input);
    }

    private int MinimumFieldWidth => LogicalToDeviceUnits(input is ThemedDateTimePicker ? 230 : input is ComboBox ? 140 : 120);

    private (bool Inline, int LabelWidth, int LabelHeight, int FieldHeight) MeasureRow(int width)
    {
        var natural = TextRenderer.MeasureText(caption.Text, caption.Font, Size.Empty, TextFormatFlags.SingleLine);
        var gap = LogicalToDeviceUnits(8);
        var inline = natural.Width + gap + MinimumFieldWidth <= width;
        var labelWidth = inline ? Math.Max(natural.Width, Math.Min(LogicalToDeviceUnits(140), width - gap - MinimumFieldWidth)) : width;
        var labelHeight = inline ? natural.Height : TextRenderer.MeasureText(caption.Text, caption.Font,
            new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak).Height;
        var fieldHeight = input is ComboBox ? input.Height : input is ThemedDateTimePicker ? Math.Max(LogicalToDeviceUnits(24), input.Font.Height + LogicalToDeviceUnits(8)) :
            input.GetPreferredSize(new Size(Math.Max(1, width - (inline ? labelWidth + gap : 0)), 0)).Height;
        return (inline, labelWidth, labelHeight, fieldHeight);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (caption == null || input == null) { return base.GetPreferredSize(proposedSize); }
        // TableLayoutPanel also probes at width 1. Use the arranged width for
        // that probe so inline rows do not reserve a stacked row's height.
        var width = proposedSize.Width > 1 && proposedSize.Width < int.MaxValue ? proposedSize.Width : Math.Max(1, Width);
        var row = MeasureRow(width);
        return new Size(width, row.Inline ? Math.Max(row.LabelHeight, row.FieldHeight) : row.LabelHeight + LogicalToDeviceUnits(4) + row.FieldHeight);
    }

    protected override void OnSizeChanged(EventArgs args)
    {
        base.OnSizeChanged(args);
        if (measuredWidth == Width) { return; }
        measuredWidth = Width;
        Parent?.PerformLayout(this, nameof(PreferredSize));
    }

    protected override void OnLayout(LayoutEventArgs args)
    {
        base.OnLayout(args);
        if (caption == null || input == null) { return; }
        var width = Math.Max(1, ClientSize.Width);
        var row = MeasureRow(width);
        if (row.Inline)
        {
            var height = Math.Max(row.LabelHeight, row.FieldHeight);
            caption.SetBounds(0, (height - row.LabelHeight) / 2, row.LabelWidth, row.LabelHeight);
            var left = row.LabelWidth + LogicalToDeviceUnits(8);
            input.SetBounds(left, (height - row.FieldHeight) / 2, Math.Max(1, width - left), row.FieldHeight);
        }
        else
        {
            caption.SetBounds(0, 0, width, row.LabelHeight);
            input.SetBounds(0, row.LabelHeight + LogicalToDeviceUnits(4), width, row.FieldHeight);
        }
    }
}
