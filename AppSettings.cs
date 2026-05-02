using System.Text.Json;

namespace YtGrab;

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string OutputFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "YtGrab");

    public bool BeepWhenDone { get; set; } = true;

    public bool OpenFolderWhenDone { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YtGrab",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            }
        }
        catch
        {
            // Fall back to defaults when settings are corrupt or inaccessible.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
