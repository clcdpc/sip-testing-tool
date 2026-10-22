using System.Text;
using Clc.Sip.Desktop.Services;
using Clc.Sip.Desktop.Tests.TestSupport;
using static Clc.Sip.Desktop.Tests.TestSupport.SessionTestData;

namespace Clc.Sip.Desktop.Tests;

[TestClass]
[TestCategory("Avalonia")]
public sealed class SettingsTests
{
    [TestMethod(DisplayName = "Legacy .sipc import and export")]
    public async Task SessionFilesImportAndExportExistingFieldNames()
    {
        const string legacy = """
            {"server":"sip.example.test","port":"5002","ao":"CLC","ap":"desk-1",
             "username":"test-user","password":"test-secret","locationCode":"MAIN","vendorProfile":"ITG",
             "patronBarcode":"P123","patronPIN":"2468","itemBarcode":"I456","bp":"6","bq":"10"}
            """;
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(legacy));
        var fields = await SettingsStore.ImportAsync(input);
        Assert.AreEqual("2468", fields.PatronPin);
        Assert.AreEqual("MAIN", fields.LocationCode);
        Assert.AreEqual("ITG", fields.VendorProfile);
        using var exported = new MemoryStream();
        await SettingsStore.ExportAsync(exported, fields);
        exported.Position = 0;
        var copy = await SettingsStore.ImportAsync(exported);
        Assert.AreEqual(fields.Server, copy.Server);
        Assert.AreEqual(fields.PatronPin, copy.PatronPin);
        Assert.AreEqual("10", copy.Bq);
        copy.Ao = "different";
        Assert.AreEqual("CLC", fields.Ao);
    }

    [TestMethod(DisplayName = "Per-user defaults survive replacement")]
    public async Task SavedDefaultsSurviveReplacementAndRejectInvalidJson()
    {
        using var scratch = new ScratchDirectory();
        var store = new SettingsStore(Path.Combine(scratch.Path, "settings.json"));
        var defaults = await store.LoadAsync();
        Assert.AreEqual("5002", defaults.Defaults.Port);
        Assert.IsFalse(defaults.ConnectOnStartup, "First startup should not connect to a server.");
        defaults.Defaults = SampleFields();
        defaults.EncodingName = "iso-8859-1";
        defaults.DarkTheme = true;
        await store.SaveAsync(defaults);
        defaults.Defaults.ItemBarcode = "changed";
        await store.SaveAsync(defaults);
        var reloaded = await store.LoadAsync();
        Assert.AreEqual("changed", reloaded.Defaults.ItemBarcode);
        Assert.AreEqual("2468", reloaded.Defaults.PatronPin);
        Assert.AreEqual("iso-8859-1", reloaded.EncodingName);
        Assert.IsTrue(reloaded.DarkTheme, "Theme was not saved.");
        Assert.AreEqual(1, Directory.GetFiles(scratch.Path).Length);
        if (!OperatingSystem.IsWindows())
        {
            Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(store.FilePath));
        }

        await File.WriteAllTextAsync(store.FilePath, "{broken");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => store.LoadAsync());
    }
}
