using Avalonia.Input;

namespace Clc.Sip.Desktop.Views;

internal sealed class DesktopShortcuts(bool isMacOS)
{
    public KeyGesture Execute { get; } = new(Key.F5);
    public KeyGesture Connect { get; } = Create(Key.C, isMacOS);
    public KeyGesture Login { get; } = Create(Key.G, isMacOS);
    public KeyGesture FocusCommand { get; } = Create(Key.K, isMacOS);
    public KeyGesture FocusPatron { get; } = Create(Key.P, isMacOS);
    public KeyGesture FocusItem { get; } = Create(Key.I, isMacOS);
    public KeyGesture FocusServer { get; } = Create(Key.L, isMacOS);
    public KeyGesture ExportLog { get; } = Create(Key.S, isMacOS);
    public KeyGesture ToggleTheme { get; } = Create(Key.D, isMacOS);
    public KeyGesture FocusMessages { get; } = Create(Key.M, isMacOS);
    public KeyGesture FindLog { get; } = Create(Key.F, isMacOS);
    public KeyGesture ClearLog { get; } = Create(Key.X, isMacOS);
    public KeyGesture ResetOptions { get; } = Create(Key.R, isMacOS);
    public KeyGesture NextPanel { get; } = new(Key.F6);
    public KeyGesture PreviousPanel { get; } = new(Key.F6, KeyModifiers.Shift);
    public KeyGesture FindNext { get; } = new(Key.F3);
    public KeyGesture FindPrevious { get; } = new(Key.F3, KeyModifiers.Shift);

    private static KeyGesture Create(Key key, bool isMacOS) => new(key,
        (isMacOS ? KeyModifiers.Meta : KeyModifiers.Control) | KeyModifiers.Shift);

    public static string Label(KeyGesture gesture) =>
        (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta) ? "Cmd+" :
            gesture.KeyModifiers.HasFlag(KeyModifiers.Control) ? "Ctrl+" : "")
        + (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "Shift+" : "") + gesture.Key;
}
