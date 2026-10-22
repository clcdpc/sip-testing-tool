using System.Runtime.InteropServices;
using Velopack;
using Velopack.Locators;

namespace Clc.Sip.Desktop.Services;

public sealed class UpdateService
{
    private UpdateManager? manager;
    private UpdateInfo? pending;
    private readonly IVelopackLocator? locator;
    private readonly string? testSource;

    public bool IsReady => pending != null;

    public UpdateService(IVelopackLocator? locator = null, string? testSource = null)
    {
        this.locator = locator;
        this.testSource = testSource;
    }

    public static string RuntimeFolder => (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux")
        + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    public static string GetFeedUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps && !(url.Scheme == Uri.UriSchemeHttp && url.IsLoopback)))
        {
            throw new ArgumentException("The update feed must use HTTPS (HTTP is allowed for localhost tests).");
        }

        return new Uri(url, RuntimeFolder + "/").AbsoluteUri;
    }

    public async Task<string> CheckAndDownloadAsync(string baseUrl)
    {
        pending = null;
        if (string.IsNullOrWhiteSpace(baseUrl) && testSource == null)
        {
            return "No update feed configured.";
        }

        manager = new UpdateManager(testSource ?? GetFeedUrl(baseUrl), locator: locator);
        if (!manager.IsInstalled)
        {
            return "Updates are available in installed releases.";
        }

        var update = await manager.CheckForUpdatesAsync();
        if (update == null)
        {
            return "You have the latest version.";
        }

        await manager.DownloadUpdatesAsync(update);
        pending = update;
        return "Update downloaded. Restart when you are ready.";
    }

    public void ApplyAndRestart()
    {
        if (manager == null || pending == null)
        {
            throw new InvalidOperationException("No update is ready to install.");
        }

        manager.ApplyUpdatesAndRestart(pending);
    }
}
