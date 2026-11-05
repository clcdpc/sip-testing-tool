using Avalonia.Input;

namespace Clc.Sip.Desktop.Views;

internal sealed class DesktopShortcuts(bool isMacOS)
{
    public KeyGesture Execute { get; } = new(Key.F5);
    public KeyGesture Connect { get; } = Create(Key.N, isMacOS);
    public KeyGesture Login { get; } = Create(Key.U, isMacOS);
    public KeyGesture FocusCommand { get; } = Create(Key.K, isMacOS);
    public KeyGesture FocusPatron { get; } = Create(Key.P, isMacOS);
    public KeyGesture FocusItem { get; } = Create(Key.I, isMacOS);
    public KeyGesture FocusServer { get; } = Create(Key.L, isMacOS);
    public KeyGesture ExportLog { get; } = Create(Key.S, isMacOS);
    public KeyGesture ToggleTheme { get; } = Create(Key.D, isMacOS);
    public KeyGesture FocusMessages { get; } = Create(Key.J, isMacOS);
    public KeyGesture FindLog { get; } = Create(Key.F, isMacOS);
    public KeyGesture ClearLog { get; } = Create(Key.X, isMacOS, shift: true);
    public KeyGesture ResetOptions { get; } = Create(Key.R, isMacOS);
    public KeyGesture NextPanel { get; } = new(Key.F6);
    public KeyGesture PreviousPanel { get; } = new(Key.F6, KeyModifiers.Shift);
    public KeyGesture FindNext { get; } = Create(Key.G, isMacOS);
    public KeyGesture FindPrevious { get; } = Create(Key.G, isMacOS, shift: true);
    public KeyGesture FindNextAlternative { get; } = new(Key.F3);
    public KeyGesture FindPreviousAlternative { get; } = new(Key.F3, KeyModifiers.Shift);
    public KeyGesture InspectMessage { get; } = Create(Key.Enter, isMacOS);
    public KeyGesture RevealPassword { get; } = Create(Key.H, isMacOS, shift: true);

    private static KeyGesture Create(Key key, bool isMacOS, bool shift = false) => new(key,
        (isMacOS ? KeyModifiers.Meta : KeyModifiers.Control) | (shift ? KeyModifiers.Shift : KeyModifiers.None));

    public static string Label(KeyGesture gesture) =>
        (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta) ? "Cmd+" :
            gesture.KeyModifiers.HasFlag(KeyModifiers.Control) ? "Ctrl+" : "")
        + (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "Shift+" : "") + gesture.Key;
}
