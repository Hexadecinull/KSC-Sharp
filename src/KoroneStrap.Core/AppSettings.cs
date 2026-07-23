using System.Text.Json;

namespace KSCSharp.Core;

public enum GraphicsApi
{
    Direct3D,
    OpenGL,
    Vulkan,
}

/// <summary>Persisted app preferences, written to settings.json in AppDataDirectory.</summary>
public class AppSettings
{
    // Activity tracking - gates both "Query server details" and (descriptively) Discord's
    // richer detail mode.
    public bool ActivityTrackingEnabled { get; set; }
    public bool QueryServerDetailsEnabled { get; set; }

    // Discord Rich Presence. Everything here defaults to the least-surprising state: off,
    // and the plainest display mode when it is turned on.
    public bool DiscordEnabled { get; set; }
    public bool DiscordShowActivity { get; set; }
    public bool DiscordShowDetails { get; set; }
    public bool DiscordAllowJoining { get; set; }
    public bool DiscordShowAccount { get; set; }

    /// <summary>
    /// Last-seen userId, from either a pekora-player:// join link's userId param or the
    /// client's own "[FLog::GameJoinLoadTime]" log line (see KoroneActivityWatcher /
    /// GameJoinInfo) - both are the client/link self-reporting this value publicly, never
    /// anything read from credential/auth storage. Unlike before, this now populates for any
    /// launch with activity tracking on, not just ones that arrived via a join link.
    /// </summary>
    public string? LastKnownUserId { get; set; }

    // Custom Integrations
    public bool WindowManipulationEnabled { get; set; }

    /// <summary>Only meaningful (and only enabled in the UI) when GraphicsApi is Vulkan.</summary>
    public bool BorderlessFullscreenVulkan { get; set; }

    // FastFlags - the management toggle defaults to true (opt-out, not opt-in), since
    // disabling it is meant to be an explicit "stop touching my client" switch.
    public bool FastFlagsManagementEnabled { get; set; } = true;

    // Global Settings > Presets > Rendering and Graphics
    public GraphicsApi GraphicsApi { get; set; } = GraphicsApi.Direct3D;
    public int FramerateLimit { get; set; } = 60;

    // FastFlags page > Presets > Geometry
    public bool MeshDetailReduced { get; set; }

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
