using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Clc.Sip.Desktop.Views;

// Keep validation on the element that actually receives focus. HelpText is mapped
// to UIA HelpText and AT-SPI accessible-description by the installed Avalonia version.
internal sealed class FieldValidationAccessibility(Action dismissError)
{
    private Control? field;
    private Control? source;
    private string? previousHelp;

    public void Show(Control control, string message, Control? previousFocus = null)
    {
        Clear();
        source = control;
        field = control switch
        {
            AccessibleAutoCompleteBox autocomplete => autocomplete.Editor ?? control,
            OptionalDateTimeInput date => date.Editor,
            _ => control
        };
        previousHelp = AutomationProperties.GetHelpText(field);
        SetHelp(field, message);
        field.PropertyChanged += FieldChanged;
        field.DetachedFromVisualTree += FieldDetached;
        if (source != field) { source.PropertyChanged += FieldChanged; }
        // Moving focus reads the new HelpText. If the editor already has focus,
        // NVDA does not read description/banner changes, including identical
        // failures. Reuse the window's live region only in that case so a single
        // validation attempt does not also repeat the focus announcement.
        if (OperatingSystem.IsWindows() && (field.IsFocused || field == previousFocus))
        {
            AccessibilityAnnouncements.Announce(field, message);
        }
    }

    public void Clear()
    {
        if (field == null) { return; }
        field.PropertyChanged -= FieldChanged;
        field.DetachedFromVisualTree -= FieldDetached;
        if (source != field && source != null) { source.PropertyChanged -= FieldChanged; }
        SetHelp(field, previousHelp);
        field = null;
        source = null;
        previousHelp = null;
    }

    private static void SetHelp(Control control, string? message)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        var previous = peer.GetHelpText();
        control.SetCurrentValue(AutomationProperties.HelpTextProperty, message);
        // Avalonia 12.1.4 does not raise this event for HelpText changes itself.
        // Emit the supported property notification, including removal of an error.
        var current = peer.GetHelpText();
        if (previous != current)
        {
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, previous, current);
        }
    }

    private void FieldChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == TextBox.TextProperty || args.Property == SelectingItemsControl.SelectedItemProperty
            || args.Property == ToggleButton.IsCheckedProperty || args.Property == OptionalDateTimeInput.IsIncludedProperty)
        {
            Clear();
            dismissError();
        }
    }

    private void FieldDetached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        Clear();
        dismissError();
    }
}
