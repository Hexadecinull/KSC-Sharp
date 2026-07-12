using System.Text.Json;

namespace KSCSharp.Core;

/// <summary>Persisted app preferences, written to settings.json in AppDataDirectory.</summary>
public class AppSettings
{
    // Discord Rich Presence. Everything here defaults to the least-surprising state: off,
    // and the plainest display mode when it is turned on.
    public bool DiscordEnabled { get; set; }
    public bool DiscordShowDetails { get; set; }
    public bool DiscordAllowJoining { get; set; }
    public bool DiscordShowAccount { get; set; }

    // Integrations
    public bool QueryServerDetailsEnabled { get; set; }
    public bool WindowManipulationEnabled { get; set; }

    // FastFlags - the management toggle defaults to true (opt-out, not opt-in), since
    // disabling it is meant to be an explicit "stop touching my client" switch.
    public bool FastFlagsManagementEnabled { get; set; } = true;

    private static string DefaultPath => Path.Combine(KoroneConfig.AppDataDirectory, "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;

        if (!File.Exists(path))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
