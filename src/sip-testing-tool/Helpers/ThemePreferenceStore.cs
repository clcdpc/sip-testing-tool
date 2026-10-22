using System.Text.Json;

namespace Clc.Sip.Helpers;

public sealed class ThemePreferenceStore
{
    public string FilePath { get; }

    public ThemePreferenceStore(string filePath = null)
        => FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Clc", "SipTestingTool", "winforms-appearance.json");

    public bool Load(bool fallback)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return fallback;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            return document.RootElement.GetProperty("darkMode").GetBoolean();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return fallback;
        }
    }

    public void Save(bool darkMode)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".appearance-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new
            {
                darkMode
            }));
            File.Move(temporary, FilePath, true);
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
