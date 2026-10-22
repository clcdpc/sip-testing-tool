using System.Text.Json;
using Clc.Sip.Desktop.Models;

namespace Clc.Sip.Desktop.Services;

public sealed class SettingsStore
{
    public string FilePath { get; }

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clc", "SipTestingTool", "settings.json");
    }

    public async Task<AppPreferences> LoadAsync()
    {
        if (!File.Exists(FilePath))
        {
            return new AppPreferences();
        }

        await using var stream = File.OpenRead(FilePath);
        var preferences = await JsonSerializer.DeserializeAsync<AppPreferences>(stream, SessionFields.JsonOptions)
            ?? throw new InvalidDataException("The defaults file is empty.");
        preferences.Defaults ??= new SessionFields();
        return preferences;
    }

    public Task SaveAsync(AppPreferences preferences) => SaveJsonAsync(FilePath, preferences);

    public static async Task<SessionFields> ImportAsync(Stream source)
        => await JsonSerializer.DeserializeAsync<SessionFields>(source, SessionFields.JsonOptions)
            ?? throw new InvalidDataException("The settings file is empty.");

    public static Task ExportAsync(Stream destination, SessionFields fields)
        => JsonSerializer.SerializeAsync(destination, fields, SessionFields.JsonOptions);

    private static async Task SaveJsonAsync<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            await using (var stream = new FileStream(temporary, options))
            {
                await JsonSerializer.SerializeAsync(stream, value, SessionFields.JsonOptions);
            }

            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
