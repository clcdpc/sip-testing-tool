using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Clc.Sip.Desktop.Views;

public sealed class AccessibleComboBox : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectionAutomationPeer(this);

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
