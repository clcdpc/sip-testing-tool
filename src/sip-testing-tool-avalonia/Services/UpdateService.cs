using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Octokit;
using StageKit.Runtime;
using StageKit.Updatum;

namespace Clc.Sip.Desktop.Services;

public sealed class UpdateService : IDisposable
{
    public const string DefaultRepositoryUrl = "https://github.com/clcdpc/sip-testing-tool";
    internal const string LegacyFeedUrl = "https://siptool.clcohio.org/updates/desktop";
    private const string ReadyMessage = "Update downloaded. Restart when you are ready.";
    private readonly Func<string, UpdatumManager> createManager;
    private readonly bool supportsUpdates;
    private readonly ReleaseVersion currentVersion;
    private readonly SemaphoreSlim operation = new(1);
    private readonly CancellationTokenSource shutdown = new();
    private UpdatumManager? manager;
    private UpdatumDownloadedAsset? pending;
    private string? pendingRepository;
    private bool disposed;

    public bool IsReady => pending != null;
    public static string RuntimeFolder => (OperatingSystem.IsWindows() ? "win" : "linux")
        + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    public UpdateService() : this(url => new UpdatumManager(url, typeof(UpdateService).Assembly.GetName().Version),
        RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
        ((OperatingSystem.IsWindows() && EntryApplication.PackagingType == ApplicationPackagingType.DotNetSingleFile)
         || (OperatingSystem.IsLinux() && EntryApplication.PackagingType == ApplicationPackagingType.LinuxAppImage))) { }

    internal UpdateService(Func<string, UpdatumManager> createManager, bool supportsUpdates, string? currentVersion = null)
    {
        this.createManager = createManager;
        this.supportsUpdates = supportsUpdates;
        // InformationalVersion retains beta identifiers and may include SDK-added commit metadata.
        this.currentVersion = currentVersion is null ? ReleaseVersion.Current : ReleaseVersion.Parse(currentVersion);
    }

    public static string GetRepositoryUrl(string value)
    {
        var text = value.Trim().TrimEnd('/');
        if (text.Equals(LegacyFeedUrl, StringComparison.OrdinalIgnoreCase)) { return DefaultRepositoryUrl; }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !Regex.IsMatch(uri.AbsolutePath, @"^/[a-zA-Z0-9-]+/[a-zA-Z0-9_.-]+$"))
        {
            throw new ArgumentException("Enter a GitHub repository URL, such as " + DefaultRepositoryUrl + ".");
        }
        return "https://github.com" + uri.AbsolutePath;
    }

    public async Task<string> CheckAndDownloadAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(repositoryUrl)) { return "No update repository configured."; }
        if (!supportsUpdates) { return "Updates are available in the portable Windows EXE and Linux AppImage."; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdown.Token);
        linked.CancelAfter(TimeSpan.FromMinutes(5));
        if (!await operation.WaitAsync(0, linked.Token)) { return "An update operation is already running."; }
        try
        {
            var repository = GetRepositoryUrl(repositoryUrl);
            if (pending != null && pendingRepository == repository) { return ReadyMessage; }
            pending?.SafeDeleteFile();
            pending = null;
            manager?.Dispose();
            manager = createManager(repository);
            manager.EventSynchronizationContext = null;
            manager.AllowPreReleases = currentVersion.IsPrerelease;
            manager.FetchOnlyLatestRelease = false;
            manager.RequireAssetChecksum = true;
            manager.AssetExtensionFilter = OperatingSystem.IsWindows() ? ".zip" : ".AppImage";
            manager.InstallUpdateWindowsExeType = UpdatumWindowsExeType.SingleFileApp;
            manager.InstallUpdateSingleFileExecutableNameStrategy = UpdatumSingleFileExecutableNameStrategy.CustomName;
            // Updatum appends the downloaded extension and treats braces as version placeholders.
            manager.InstallUpdateSingleFileExecutableName = Path.GetFileNameWithoutExtension(EntryApplication.ExecutablePath)
                ?.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
            manager.InstallUpdateInjectCustomScript = CreateExitWaitScript(Environment.ProcessId, OperatingSystem.IsWindows());
            // Updatum fetches releases, but its System.Version comparison ignores beta.1/beta.2.
            // Select from the unfiltered releases ourselves and pass the exact release to download.
            await manager.CheckForUpdatesAsync(linked.Token);
            var release = SelectRelease(manager);
            if (release is null) { return "You have the latest version."; }
            var downloaded = await manager.DownloadUpdateAsync(release, linked.Token)
                ?? throw new InvalidOperationException("The release has no compatible download.");
            if (!downloaded.IsChecksumVerified)
            {
                downloaded.SafeDeleteFile();
                throw new InvalidDataException("The update download was not verified.");
            }
            if (linked.IsCancellationRequested)
            {
                downloaded.SafeDeleteFile();
                linked.Token.ThrowIfCancellationRequested();
            }
            pending = downloaded;
            pendingRepository = repository;
            return ReadyMessage;
        }
        finally { operation.Release(); }
    }

    private Release? SelectRelease(UpdatumManager updater)
    {
        var candidates = new List<(Release Release, ReleaseVersion Version)>();
        foreach (var release in updater.Releases)
        {
            if (release.Draft || release.PublishedAt is null || release.TagName is not { Length: > 1 } tag
                || tag[0] != 'v' || !ReleaseVersion.TryParse(tag[1..], out var version)
                || version.CompareTo(currentVersion) <= 0
                || !currentVersion.IsPrerelease && (release.Prerelease || version.IsPrerelease))
            {
                continue;
            }
            candidates.Add((release, version));
        }
        foreach (var candidate in candidates.OrderByDescending(candidate => candidate.Version))
        {
            var name = $"SIP-Testing-Tool_{RuntimeFolder}_v{candidate.Version.Value}{updater.AssetExtensionFilter}";
            updater.AssetRegexPattern = @"\A" + Regex.Escape(name) + @"\z";
            if (updater.GetCompatibleReleaseAsset(candidate.Release) != null) { return candidate.Release; }
        }
        return null;
    }

    public async Task PrepareRestartAsync(string repositoryUrl)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!await operation.WaitAsync(0)) { throw new InvalidOperationException("An update operation is already running."); }
        try
        {
            if (manager == null || pending == null) { throw new InvalidOperationException("No update is ready to install."); }
            if (string.IsNullOrWhiteSpace(repositoryUrl) || pendingRepository != GetRepositoryUrl(repositoryUrl))
            {
                pending.SafeDeleteFile();
                pending = null;
                throw new InvalidOperationException("The update repository changed. Check for updates again.");
            }
            // The generated helper waits for this PID. Avalonia owns normal shutdown and disposal.
            if (!await manager.InstallUpdateAsync(pending, forceTerminate: false, runArguments: null, shutdown.Token))
            {
                throw new IOException("The update helper could not be started.");
            }
            pending = null; // The helper now owns the download and its cleanup.
        }
        catch (InvalidDataException)
        {
            pending?.SafeDeleteFile();
            pending = null;
            throw;
        }
        finally { operation.Release(); }
    }

    internal static string CreateExitWaitScript(int processId, bool windows) => windows
        ? $$"""
            powershell.exe -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; try { $p=[System.Diagnostics.Process]::GetProcessById({{processId}}); if (-not $p.WaitForExit(120000)) { exit 1 } } catch { if ($_.Exception.GetBaseException() -is [System.ArgumentException]) { exit 0 }; exit 1 }"
            if errorlevel 1 goto :ScriptFailed
            """
        : $$"""
            for ((attempt=0; attempt<120; attempt++)); do
              if ! kill -0 {{processId}} 2>/dev/null; then break; fi
              sleep 1
            done
            if kill -0 {{processId}} 2>/dev/null; then exit 1; fi
            """;

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        shutdown.Cancel();
        manager?.Dispose();
        pending?.SafeDeleteFile();
        pending = null;
    }
}
