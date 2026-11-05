using Avalonia.Controls;

namespace Clc.Sip.Desktop.Views;

// One invisible announcement per window, using Avalonia's supported live-region
// mapping. The pinned Windows bridge drops ValuePattern events and NVDA does not
// announce ItemStatus changes on edits. Other platforms retain their normal peers.
internal static class AccessibilityAnnouncements
{
    public static void Announce(Control source, string? message)
    {
        if (string.IsNullOrEmpty(message)) { return; }
        // Declare the region in each window before it is shown, like the
        // documented live-region example. Do not add a new automation subtree
        // during keyboard navigation.
        var region = TopLevel.GetTopLevel(source)?.FindControl<TextBlock>("AccessibilityAnnouncement");
        if (region == null) { return; }
        // Identical messages still need a fresh text/name change. The empty
        // intermediate name carries no spoken content.
        if (region.Text == message) { region.Text = ""; }
        region.Text = message;
    }
}
