using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.Tests.TestSupport;
using Octokit;
using StageKit.Updatum;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
[TestCategory("Avalonia")]
public sealed class UpdateServiceTests
{
    [TestMethod]
    public void RepositoryUrlsAreValidatedAndLegacyDefaultIsMigrated()
    {
        Assert.AreEqual(UpdateService.DefaultRepositoryUrl, UpdateService.GetRepositoryUrl(" https://github.com/clcdpc/sip-testing-tool/ "));
        Assert.AreEqual(UpdateService.DefaultRepositoryUrl, UpdateService.GetRepositoryUrl(UpdateService.LegacyFeedUrl + "/"));
        foreach (var invalid in new[] { "http://github.com/a/b", "https://example.test/a/b", "https://github.com/a", "https://github.com/a/b/releases", "https://user@github.com/a/b", "https://github.com/a/b?x=1", "file:///tmp/updates" })
        {
            Assert.Throws<ArgumentException>(() => UpdateService.GetRepositoryUrl(invalid));
        }
    }

    [TestMethod]
    public async Task SavedLegacyFeedMovesToGitHubWithoutChangingOtherPreferences()
    {
        using var scratch = new ScratchDirectory();
        var store = new SettingsStore(Path.Combine(scratch.Path, "settings.json"));
        await File.WriteAllTextAsync(store.FilePath, """{"updateFeedUrl":"https://siptool.clcohio.org/updates/desktop/","darkTheme":true,"checkUpdatesOnStartup":false}""");
        var preferences = await store.LoadAsync();
        Assert.AreEqual(UpdateService.DefaultRepositoryUrl, preferences.UpdateFeedUrl);
        Assert.IsTrue(preferences.DarkTheme);
        Assert.IsFalse(preferences.CheckUpdatesOnStartup);
        preferences.UpdateFeedUrl = "";
        await store.SaveAsync(preferences);
        Assert.AreEqual("", (await store.LoadAsync()).UpdateFeedUrl);
    }

    [TestMethod]
    public async Task DevelopmentBuildsDoNotRequestUpdatesOrRestart()
    {
        using var updates = new UpdateService();
        Assert.AreEqual("Updates are available in the portable Windows EXE and Linux AppImage.", await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl));
        Assert.IsFalse(updates.IsReady);
        await Assert.ThrowsAsync<InvalidOperationException>(() => updates.PrepareRestartAsync(UpdateService.DefaultRepositoryUrl));
    }

    [TestMethod]
    public async Task VerifiedGitHubDownloadBecomesReadyAndIsReusedUntilRestart()
    {
        using var fixture = new UpdateFixture();
        using var updates = fixture.CreateService();
        Assert.AreEqual("Update downloaded. Restart when you are ready.", await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl));
        Assert.IsTrue(updates.IsReady);
        Assert.AreEqual(1, fixture.DownloadCount);
        Assert.AreEqual("Update downloaded. Restart when you are ready.", await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl));
        Assert.AreEqual(1, fixture.DownloadCount);
        Assert.IsTrue(fixture.Manager!.RequireAssetChecksum);
        Assert.IsFalse(fixture.Manager.AllowPreReleases);
        Assert.AreEqual(UpdatumWindowsExeType.SingleFileApp, fixture.Manager.InstallUpdateWindowsExeType);
        Assert.AreEqual(UpdatumSingleFileExecutableNameStrategy.CustomName, fixture.Manager.InstallUpdateSingleFileExecutableNameStrategy);
    }

    [TestMethod]
    public async Task InvalidOrMissingChecksumsNeverEnableRestart()
    {
        foreach (var digest in new string?[] { new string('0', 64), null })
        {
            using var fixture = new UpdateFixture(digest);
            using var updates = fixture.CreateService();
            await Assert.ThrowsAsync<InvalidDataException>(() => updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl));
            Assert.IsFalse(updates.IsReady);
            await Assert.ThrowsAsync<InvalidOperationException>(() => updates.PrepareRestartAsync(UpdateService.DefaultRepositoryUrl));
        }
    }

    [TestMethod]
    public async Task DraftsPrereleasesAndOtherPlatformsAreIgnored()
    {
        using var fixture = new UpdateFixture(onlyIneligible: true);
        using var updates = fixture.CreateService();
        Assert.AreEqual("You have the latest version.", await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl));
        Assert.IsFalse(updates.IsReady);
        Assert.AreEqual(0, fixture.DownloadCount);
    }

    [TestMethod]
    [DataRow("2.1.2-beta.1", "2.1.2-beta.2")]
    [DataRow("2.1.2-beta.2", "2.1.2-beta.10")]
    [DataRow("2.1.2-beta.10", "2.1.2-rc.1")]
    [DataRow("2.1.2-beta.2+commit.123", "2.1.2")]
    [DataRow("2.1.2-beta.2", "2.1.3-beta.1")]
    [DataRow("2.1.2", "2.1.3")]
    public async Task NewerSemanticVersionIsDownloaded(string current, string next)
    {
        using var fixture = new UpdateFixture(currentVersion: current,
            releases: [new(next, Prerelease: next.Contains('-'))]);
        using var updates = fixture.CreateService();
        await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl);
        Assert.IsTrue(updates.IsReady);
        Assert.AreEqual("v" + next, fixture.Download!.Release.TagName);
        Assert.AreEqual(current.Contains('-'), fixture.Manager!.AllowPreReleases);
        Assert.IsFalse(fixture.Manager.FetchOnlyLatestRelease);
    }

    [TestMethod]
    [DataRow("2.1.2-beta.10", "2.1.2-beta.2", true)]
    [DataRow("2.1.2-beta.2", "2.1.2-beta.2", true)]
    [DataRow("2.1.2-beta.2+commit.one", "2.1.2-beta.2+commit.two", true)]
    [DataRow("2.1.2-beta.2", "2.1.1", false)]
    [DataRow("2.1.2", "2.1.2-beta.10", true)]
    [DataRow("2.1.2", "2.1.3-beta.1", true)]
    [DataRow("2.1.2", "2.1.3-beta.1", false)]
    [DataRow("2.1.2", "2.1.3", true)]
    public async Task EqualOlderAndOutOfChannelReleasesAreNotDownloaded(string current, string candidate, bool prerelease)
    {
        using var fixture = new UpdateFixture(currentVersion: current, releases: [new(candidate, Prerelease: prerelease)]);
        using var updates = fixture.CreateService();
        Assert.AreEqual("You have the latest version.", await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl));
        Assert.IsFalse(updates.IsReady);
        Assert.AreEqual(0, fixture.DownloadCount);
    }

    [TestMethod]
    public async Task HighestCompatibleSemanticVersionWinsRegardlessOfReleaseOrder()
    {
        using var fixture = new UpdateFixture(currentVersion: "2.1.2-beta.1", releases:
        [
            new("2.1.2-beta.2", Prerelease: true),
            new("2.1.2-rc.1", Prerelease: true),
            new("2.1.2-beta.11", Prerelease: true),
            new("2.1.2"),
            new("2.1.2-beta.10", Prerelease: true)
        ]);
        using var updates = fixture.CreateService();
        await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl);
        Assert.AreEqual("v2.1.2", fixture.Download!.Release.TagName);
        Assert.AreEqual(1, fixture.DownloadCount);
    }

    [TestMethod]
    public async Task BetaBuildsIgnoreDraftsUnpublishedInvalidAndMismatchedDownloads()
    {
        using var fixture = new UpdateFixture(currentVersion: "2.1.2-beta.1", releases:
        [
            new("9.0.0-beta.1", Draft: true, Prerelease: true),
            new("8.0.0-beta.1", Prerelease: true, Published: false),
            new("7.0.0-beta.1", Prerelease: true, Rid: "unsupported-rid"),
            new("6.0.0-beta.1", Prerelease: true, AssetVersion: "6.0.0"),
            new("5.0.0-beta.01", Prerelease: true),
            new("4.0.0-beta.1", Prerelease: true, Tag: "release-4.0.0-beta.1"),
            new("2.1.2-beta.2", Prerelease: true)
        ]);
        using var updates = fixture.CreateService();
        await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl);
        Assert.AreEqual("v2.1.2-beta.2", fixture.Download!.Release.TagName);
    }

    [TestMethod]
    public async Task StableBuildSelectsStableReleaseEvenWhenANewerBetaExists()
    {
        using var fixture = new UpdateFixture(currentVersion: "2.1.2", releases:
        [
            new("2.2.0-beta.1", Prerelease: true),
            new("2.1.3"),
            new("2.1.4-beta.1") // A missing GitHub prerelease flag must not expose a beta to stable users.
        ]);
        using var updates = fixture.CreateService();
        await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl);
        Assert.AreEqual("v2.1.3", fixture.Download!.Release.TagName);
    }

    [TestMethod]
    public async Task TamperingAfterDownloadInvalidatesThePendingUpdate()
    {
        using var fixture = new UpdateFixture();
        using var updates = fixture.CreateService();
        await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl);
        await File.AppendAllTextAsync(fixture.Download!.FilePath, "changed after verification");
        await Assert.ThrowsAsync<InvalidDataException>(() => updates.PrepareRestartAsync(UpdateService.DefaultRepositoryUrl));
        Assert.IsFalse(updates.IsReady);
        Assert.IsFalse(fixture.Download.FileExists);
    }

    [TestMethod]
    public async Task ChangingTheRepositoryDiscardsThePreviousDownload()
    {
        using var fixture = new UpdateFixture();
        using var updates = fixture.CreateService();
        await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl);
        await Assert.ThrowsAsync<InvalidOperationException>(() => updates.PrepareRestartAsync("https://github.com/local/another-repository"));
        Assert.IsFalse(updates.IsReady);
        Assert.IsFalse(fixture.Download!.FileExists);
    }

    [TestMethod]
    public async Task CancellingOrDisposingDownloadDoesNotLeaveAnUpdateReady()
    {
        using var fixture = new UpdateFixture(blockDownload: true);
        using var updates = fixture.CreateService();
        var check = updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl);
        await fixture.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("An update operation is already running.", await updates.CheckAndDownloadAsync(UpdateService.DefaultRepositoryUrl));
        await Assert.ThrowsAsync<InvalidOperationException>(() => updates.PrepareRestartAsync(UpdateService.DefaultRepositoryUrl));
        updates.Dispose();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await check);
        Assert.IsFalse(updates.IsReady);
    }

    // Exercise the released Updatum client, with GitHub and download transport replaced locally.
    private sealed record ReleaseSpec(string Version, bool Draft = false, bool Prerelease = false,
        string? Rid = null, bool Published = true, string? AssetVersion = null, string? Tag = null);

    private sealed class UpdateFixture : IDisposable
    {
        private readonly byte[] payload = "local verified update fixture"u8.ToArray();
        private readonly string? digest;
        private readonly ReleaseSpec[] releases;
        private readonly string currentVersion;
        private readonly bool blockDownload;
        private readonly HttpClient http;
        public int DownloadCount { get; private set; }
        public UpdatumManager? Manager { get; private set; }
        public UpdatumDownloadedAsset? Download { get; private set; }
        public TaskCompletionSource DownloadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public UpdateFixture(string? digest = "valid", bool onlyIneligible = false, bool blockDownload = false,
            string currentVersion = "2.1.1", ReleaseSpec[]? releases = null)
        {
            this.digest = digest == "valid" ? Convert.ToHexString(SHA256.HashData(payload)) : digest;
            this.releases = releases ?? (onlyIneligible
                ? [new("99.0.0", Draft: true), new("99.0.0", Prerelease: true), new("99.0.0", Rid: "unsupported-rid")]
                : [new("99.0.0")]);
            this.currentVersion = currentVersion;
            this.blockDownload = blockDownload;
            http = new HttpClient(new AssetHandler(this));
        }

        public UpdateService CreateService() => new(_ =>
        {
            var github = new GitHubClient(new Connection(new ProductHeaderValue("SipUpdateTests"), new MockGitHub(request =>
            {
                if (request.Endpoint.ToString().Contains("/assets/"))
                    return new Response(JsonSerializer.Serialize(new { digest = digest == null ? null : "sha256:" + digest }));
                return new Response(JsonSerializer.Serialize(releases.Select((release, index) => Release(release, index + 1))));
            })));
            var constructor = typeof(UpdatumManager).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string), typeof(GitHubClient)], null)!;
            Manager = (UpdatumManager)constructor.Invoke(["local", "fixture", github]);
            typeof(UpdatumManager).GetProperty(nameof(UpdatumManager.AssetHttpClient))!.SetValue(Manager, http);
            Manager.DownloadCompleted += (_, download) => Download = download;
            return Manager;
        }, true, currentVersion);

        private object Release(ReleaseSpec release, int id)
        {
            var name = "SIP-Testing-Tool_" + (release.Rid ?? UpdateService.RuntimeFolder) + "_v" + (release.AssetVersion ?? release.Version)
                + (OperatingSystem.IsWindows() ? ".zip" : ".AppImage");
            var tag = release.Tag ?? "v" + release.Version;
            return new
            {
                url = $"https://api.github.com/repos/local/fixture/releases/{id}",
                html_url = "https://github.com/local/fixture/releases/tag/" + tag,
                assets_url = $"https://api.github.com/repos/local/fixture/releases/{id}/assets",
                upload_url = $"https://uploads.github.com/repos/local/fixture/releases/{id}/assets",
                id, node_id = "fixture", tag_name = tag, target_commitish = "master", name = "Fixture", body = "",
                draft = release.Draft, prerelease = release.Prerelease, created_at = DateTimeOffset.UtcNow,
                published_at = release.Published ? (DateTimeOffset?)DateTimeOffset.UtcNow : null,
                assets = new[] { new
                {
                    url = $"https://api.github.com/repos/local/fixture/releases/assets/{id}", id, node_id = "asset",
                    name, label = "", state = "uploaded", content_type = "application/octet-stream", size = payload.Length,
                    download_count = 0, created_at = DateTimeOffset.UtcNow, updated_at = DateTimeOffset.UtcNow,
                    browser_download_url = "https://update-fixture.invalid/" + name
                }}
            };
        }

        public void Dispose() => http.Dispose();

        private sealed class AssetHandler(UpdateFixture fixture) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.AreEqual("update-fixture.invalid", request.RequestUri!.Host);
                fixture.DownloadCount++;
                fixture.DownloadStarted.TrySetResult();
                if (fixture.blockDownload) { await Task.Delay(Timeout.Infinite, cancellationToken); }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture.payload) };
            }
        }
        private sealed class MockGitHub(Func<Octokit.Internal.IRequest, IResponse> response) : Octokit.Internal.IHttpClient
        {
            public Task<IResponse> Send(Octokit.Internal.IRequest request, CancellationToken cancellationToken, Func<object, object> postProcessing) => Task.FromResult(response(request));
            public void SetRequestTimeout(TimeSpan timeout) { }
            public void Dispose() { }
        }
        private sealed class Response(string body) : IResponse
        {
            public object Body => body;
            public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>();
            public ApiInfo ApiInfo { get; } = new(new Dictionary<string, Uri>(), [], [], "", new RateLimit(), TimeSpan.Zero);
            public HttpStatusCode StatusCode => HttpStatusCode.OK;
            public string ContentType => "application/json";
        }
    }
}
