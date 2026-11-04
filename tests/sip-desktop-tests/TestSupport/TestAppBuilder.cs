using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Headless;
using Avalonia.Input;

namespace Clc.Sip.Desktop.Tests.TestSupport;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().WithInterFont().With<IStorageProviderFactory>(new TestStorageProviderFactory())
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true })
        // Headless defaults to Control on every OS. Match the native backend so
        // Command editing shortcuts are exercised on macOS, too.
        .AfterSetup(_ => ConfigurePlatformHotkeys(OperatingSystem.IsMacOS()));

    internal static void ConfigurePlatformHotkeys(bool isMacOS)
    {
        var hotkeys = Application.Current!.PlatformSettings!.HotkeyConfiguration;
        var primary = isMacOS ? KeyModifiers.Meta : KeyModifiers.Control;
        hotkeys.CommandModifiers = primary;
        hotkeys.WholeWordTextActionModifiers = isMacOS ? KeyModifiers.Alt : KeyModifiers.Control;
        foreach (var gestures in new[] { hotkeys.Copy, hotkeys.Cut, hotkeys.Paste, hotkeys.SelectAll, hotkeys.Undo, hotkeys.Redo })
        {
            gestures[0] = new KeyGesture(gestures[0].Key, primary);
        }
        hotkeys.Redo[1] = new KeyGesture(Key.Z, primary | KeyModifiers.Shift);
    }
}
