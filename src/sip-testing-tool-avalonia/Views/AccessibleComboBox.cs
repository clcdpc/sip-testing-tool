using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Clc.Sip.Desktop.Views;

public sealed class AccessibleComboBox : ComboBox
{
    private double? requiredWidth;

    public AccessibleComboBox()
    {
        Items.CollectionChanged += (_, _) => InvalidateRequiredWidth();
        ActualThemeVariantChanged += (_, _) => InvalidateRequiredWidth();
    }

    protected override Type StyleKeyOverride => typeof(ComboBox);
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectionAutomationPeer(this);

    internal double RequiredWidth
    {
        get
        {
            ApplyStyling();
            ApplyTemplate();
            return requiredWidth ??= MeasureOptions();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(Math.Min(availableSize.Width, Math.Max(size.Width, RequiredWidth)), size.Height);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemTemplateProperty || change.Property == SelectionBoxItemTemplateProperty ||
            change.Property == DisplayMemberBindingProperty || change.Property == TemplateProperty ||
            change.Property == ThemeProperty ||
            change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
            change.Property == FontStyleProperty || change.Property == FontWeightProperty ||
            change.Property == FontStretchProperty || change.Property == FontFeaturesProperty ||
            change.Property == LetterSpacingProperty || change.Property == FlowDirectionProperty ||
            change.Property == PaddingProperty || change.Property == BorderThicknessProperty ||
            change.Property == MinWidthProperty || change.Property == UseLayoutRoundingProperty)
        {
            InvalidateRequiredWidth();
        }
    }

    private void InvalidateRequiredWidth()
    {
        requiredWidth = null;
        InvalidateMeasure();
    }

    private double MeasureOptions()
    {
        // Measure the complete themed control, including its padding and indicator.
        // This logical-only probe inherits resources without adding a visual or an
        // automation peer, or changing the real control's selection or focus.
        var probe = new ComboBox
        {
            ItemsSource = Items,
            ItemTemplate = ItemTemplate,
            DisplayMemberBinding = DisplayMemberBinding,
            SelectionBoxItemTemplate = SelectionBoxItemTemplate,
            Template = Template,
            Theme = Theme,
            FontFamily = FontFamily,
            FontSize = FontSize,
            FontStyle = FontStyle,
            FontWeight = FontWeight,
            FontStretch = FontStretch,
            FontFeatures = FontFeatures,
            LetterSpacing = LetterSpacing,
            FlowDirection = FlowDirection,
            Padding = Padding,
            BorderThickness = BorderThickness,
            MinWidth = MinWidth,
            UseLayoutRounding = UseLayoutRounding
        };
        LogicalChildren.Add(probe);
        try
        {
            var width = MinWidth;
            for (var index = 0; index < Items.Count; index++)
            {
                probe.SelectedIndex = index;
                // Detached visuals do not receive a layout pass between choices.
                LayoutHelper.InvalidateSelfAndChildrenMeasure(probe);
                probe.Measure(Size.Infinity);
                width = Math.Max(width, probe.DesiredSize.Width);
            }
            return Math.Ceiling(width);
        }
        finally
        {
            LogicalChildren.Remove(probe);
            probe.Template = null;
            probe.ApplyTemplate();
            probe.ItemsSource = null;
        }
    }

    private sealed class SelectionAutomationPeer : ComboBoxAutomationPeer
    {
        private string? previousValue;

        public SelectionAutomationPeer(ComboBox owner) : base(owner)
        {
            previousValue = ((IValueProvider)this).Value;
        }

        protected override void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            base.OwnerPropertyChanged(sender, e);
            if (e.Property == SelectedItemProperty && !Owner.IsEditable)
            {
                // Avalonia 12.1.4 only notifies Value changes for editable text.
                // Retain its selection/expansion providers and add the missing event.
                var value = ((IValueProvider)this).Value;
                if (previousValue != value)
                {
                    var oldValue = previousValue;
                    previousValue = value;
                    RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldValue, value);
                    // Avalonia 12.1.4's Win32 bridge does not map ValuePattern
                    // changes to UIA. Expanded items already announce focus.
                    if (OperatingSystem.IsWindows() && Owner.IsFocused && !Owner.IsDropDownOpen)
                    {
                        AccessibilityAnnouncements.Announce(Owner, value);
                    }
                }
            }
            else if (e.Property == IsEditableProperty || (e.Property == TextProperty && Owner.IsEditable))
            {
                previousValue = ((IValueProvider)this).Value;
            }
        }
    }
}
