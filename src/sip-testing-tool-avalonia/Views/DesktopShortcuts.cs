using Avalonia.Input;

namespace Clc.Sip.Desktop.Views;

internal sealed class DesktopShortcuts(bool isMacOS)
{
    public KeyGesture Execute { get; } = new(Key.F5);
    public KeyGesture FocusCommand { get; } = Create(Key.K, false, isMacOS);
    public KeyGesture FocusPatron { get; } = Create(Key.P, true, isMacOS);
    public KeyGesture FocusItem { get; } = Create(Key.I, true, isMacOS);
    public KeyGesture FocusServer { get; } = Create(Key.L, false, isMacOS);
    public KeyGesture ExportLog { get; } = Create(Key.S, true, isMacOS);
    public KeyGesture ToggleTheme { get; } = Create(Key.D, true, isMacOS);
    public KeyGesture FocusMessages { get; } = Create(Key.M, true, isMacOS);

    private static KeyGesture Create(Key key, bool shift, bool isMacOS) => new(key,
        (isMacOS ? KeyModifiers.Meta : KeyModifiers.Control) | (shift ? KeyModifiers.Shift : KeyModifiers.None));

    public static string Label(KeyGesture gesture) =>
        (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta) ? "Cmd+" :
            gesture.KeyModifiers.HasFlag(KeyModifiers.Control) ? "Ctrl+" : "")
        + (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "Shift+" : "") + gesture.Key;
}
