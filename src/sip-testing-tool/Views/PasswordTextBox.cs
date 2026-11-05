using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Clc.Sip.Desktop.Views;

public sealed class PasswordTextBox : TextBox
{
    public static readonly StyledProperty<bool> IsPinProperty =
        AvaloniaProperty.Register<PasswordTextBox, bool>(nameof(IsPin));
    public bool IsPin { get => GetValue(IsPinProperty); set => SetValue(IsPinProperty, value); }
    private readonly KeyGesture revealShortcut = new DesktopShortcuts(OperatingSystem.IsMacOS()).RevealPassword;
    private bool changingFocus;
    private string SecretName => IsPin ? "PIN" : "Password";
    internal string RevealDescription => DesktopShortcuts.Label(revealShortcut) + " to show or hide " + (IsPin ? "PIN." : "password.");
    private string VisibilityStatus => SecretName + (RevealPassword ? " shown" : " hidden");
    protected override Type StyleKeyOverride => typeof(TextBox);

    public PasswordTextBox()
    {
        PasswordChar = '●';
        Classes.Add("revealPasswordButton");
        AddHandler(KeyDownEvent, RevealKeyDown, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => SetCurrentValue(RevealPasswordProperty, false);
        UpdateVisibilityStatus();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PasswordAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // InnerRightContent is created by Fluent's existing nonempty-field style.
        Dispatcher.UIThread.Post(ConfigureRevealButton);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == RevealPasswordProperty && !changingFocus) || change.Property == IsPinProperty)
        {
            UpdateVisibilityStatus();
            if (change.Property == RevealPasswordProperty && OperatingSystem.IsWindows() && IsFocused)
            {
                // NVDA does not subscribe to ItemStatus on edit controls. Use
                // the live-region bridge for the editor; a focused eye reports
                // its own toggle state, so it needs no additional announcement.
                AccessibilityAnnouncements.Announce(this, VisibilityStatus);
            }
        }
        if (change.Property == InnerRightContentProperty || change.Property == TextProperty)
        {
            Dispatcher.UIThread.Post(ConfigureRevealButton);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        var revealed = RevealPassword;
        // TextBox itself hides on every LostFocus, including a transfer to its
        // own eye. Retain its editing cleanup, then use the actual focus target.
        changingFocus = true;
        try
        {
            base.OnLostFocus(e);
            var within = e.NewFocusedElement is Visual next && (next == this || this.IsVisualAncestorOf(next));
            SetCurrentValue(RevealPasswordProperty, revealed && within);
        }
        finally { changingFocus = false; }
        UpdateVisibilityStatus();
    }

    private void RevealKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsKeyboardFocusWithin && revealShortcut.Matches(e))
        {
            SetCurrentValue(RevealPasswordProperty, !RevealPassword);
            e.Handled = true;
        }
    }

    private void UpdateVisibilityStatus()
    {
        // ItemStatus has a supported UIA property-change mapping. Never place the
        // secret in a name, description, notification, or value-change event.
        AutomationProperties.SetItemStatus(this, VisibilityStatus);
        ConfigureRevealButton();
    }

    private void ConfigureRevealButton()
    {
        var button = this.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault();
        if (button == null) { return; }
        AutomationProperties.SetName(button, "Show " + (IsPin ? "PIN" : "password"));
        AutomationProperties.SetHelpText(button, "Checked when " + (IsPin ? "PIN" : "password") + " is shown. " + RevealDescription);
        foreach (var icon in button.GetVisualDescendants().OfType<PathIcon>())
        {
            AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        }
    }

    private sealed class PasswordAutomationPeer : ControlAutomationPeer, IValueProvider
    {
        private readonly PasswordTextBox owner;

        public PasswordAutomationPeer(PasswordTextBox owner) : base(owner)
        {
            this.owner = owner;
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextProperty)
                {
                    RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty,
                        Mask(e.OldValue as string), Mask(e.NewValue as string));
                }
            };
        }

        // The stock TextBox peer exposes plaintext even with PasswordChar set.
        // Keep automation masked even during visual reveal, avoiding unsolicited speech.
        public bool IsReadOnly => owner.IsReadOnly;
        public string Value => Mask(owner.Text);
        public void SetValue(string? value) => owner.SetCurrentValue(TextProperty, value);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;
        protected override string GetLocalizedControlTypeCore() => "password";
        protected override string? GetPlaceholderTextCore() => owner.PlaceholderText;
        protected override string? GetHelpTextCore()
        {
            var description = base.GetHelpTextCore();
            var visibility = owner.VisibilityStatus + ". " + owner.RevealDescription;
            return string.IsNullOrWhiteSpace(description) ? visibility : description + " " + visibility;
        }

        private string Mask(string? text) => new(owner.PasswordChar, text?.Length ?? 0);
    }
}
