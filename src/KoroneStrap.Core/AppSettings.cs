using System.Text.Json;

namespace KSCSharp.Core;

/// <summary>Persisted app preferences - currently just the Discord Rich Presence toggles.</summary>
public class AppSettings
{
    public bool DiscordEnabled { get; set; }
    public bool DiscordShowDetails { get; set; } = true;
    public bool DiscordAllowJoining { get; set; } = true;
    public bool DiscordShowAccount { get; set; }

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
