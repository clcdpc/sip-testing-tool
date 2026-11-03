using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Headless;

namespace Clc.Sip.Desktop.Tests.TestSupport;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().WithInterFont().With<IStorageProviderFactory>(new TestStorageProviderFactory())
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true });
}
