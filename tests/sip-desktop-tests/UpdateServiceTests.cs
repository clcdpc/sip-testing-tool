using Clc.Sip.Desktop.Services;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
[TestCategory("Avalonia")]
public sealed class UpdateServiceTests
{
    [TestMethod(DisplayName = "Update feeds select the current platform")]
    public void UpdateFeedsSelectCurrentPlatformAndValidateScheme()
    {
        Assert.AreEqual("https://example.test/updates/" + UpdateService.RuntimeFolder + "/", UpdateService.GetFeedUrl("https://example.test/updates"));
        Assert.IsTrue(UpdateService.GetFeedUrl("http://127.0.0.1:1234/feed/").EndsWith(UpdateService.RuntimeFolder + "/"), "Local test feed was rejected.");
        Assert.Throws<ArgumentException>(() => UpdateService.GetFeedUrl("http://example.test/updates"));
        Assert.Throws<ArgumentException>(() => UpdateService.GetFeedUrl("file:///tmp/updates"));
    }

    [TestMethod(DisplayName = "Uninstalled builds do not request updates")]
    public async Task UninstalledBuildsDoNotRequestUpdatesOrRestart()
    {
        var updates = new UpdateService();
        Assert.AreEqual("Updates are available in installed releases.", await updates.CheckAndDownloadAsync("https://unreachable.invalid/updates/"));
        Assert.IsFalse(updates.IsReady, "Development builds should not have a pending update.");
        Assert.Throws<InvalidOperationException>(updates.ApplyAndRestart);
    }
}
