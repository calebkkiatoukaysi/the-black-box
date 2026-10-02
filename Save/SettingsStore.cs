using System;
using System.IO;
using System.Text.Json;

namespace TheBlackBox;

/// <summary>
/// Reads and writes settings.json, next to the Saves folder.
/// </summary>
/// <remarks>Same rules as SaveSystem: written through a .tmp so a crash cannot half-write it, and nothing throws. A bad file just means the defaults.</remarks>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Where the file lives.</summary>
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheBlackBox", "settings.json");

    /// <summary>When true nothing is written. The proof run sets it so it never touches the real file.</summary>
    public static bool ReadOnly { get; set; }

    /// <summary>Reads the settings, or the defaults if there are none or they will not read.</summary>
    public static Settings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Settings();
            return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options) ?? new Settings()).Clamped();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    /// <summary>Writes the settings. A failure is ignored, since the game still works on the values in memory.</summary>
    /// <param name="settings">What to write.</param>
    public static void Save(Settings settings)
    {
        if (ReadOnly || settings is null) return;

        string temporary = FilePath + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Don't leave a half-written temp file behind. If even that fails there is nothing more to do.
            try { File.Delete(temporary); }
            catch (Exception) { }
        }
    }
}
