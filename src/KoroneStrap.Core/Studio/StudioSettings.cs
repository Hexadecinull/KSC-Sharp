using System.Text.Json;

namespace KSCSharp.Core.Studio;

public class StudioInstallInfo
{
    public string? Path { get; set; }
    public string? LastKnownRemoteFingerprint { get; set; }
}

/// <summary>Persisted Studio install locations, one entry per year ("2017", "2018", "2020", "2021").</summary>
public class StudioSettings
{
    public Dictionary<string, StudioInstallInfo> Installs { get; set; } = new();

    private static string DefaultPath => Path.Combine(KoroneConfig.AppDataDirectory, "studio.json");

    public static StudioSettings Load(string? path = null)
    {
        path ??= DefaultPath;

        if (!File.Exists(path))
            return new StudioSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<StudioSettings>(json) ?? new StudioSettings();
        }
        catch (Exception)
        {
            return new StudioSettings();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public StudioInstallInfo GetOrCreate(string year)
    {
        if (!Installs.TryGetValue(year, out var info))
        {
            info = new StudioInstallInfo();
            Installs[year] = info;
        }
        return info;
    }
}
