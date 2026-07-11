using System.Text.Json;
using KSCSharp.Core.Platform;

namespace KSCSharp.Core;

public record ApplyResult(int TargetsWritten, IReadOnlyList<string> Failures);

/// <summary>
/// Manages FastFlags.
///
/// There are two distinct things this touches, and they were previously conflated:
///  - a local cache file (fastFlags.json next to the app) used purely so the UI has
///    something to load/edit/persist between runs, and
///  - the actual ClientAppSettings.json inside each installed client's ClientSettings folder,
///    which is what Pekora/Roblox itself reads at startup. Writing only the former (which is
///    all the previous port did) has no effect on the running game.
/// </summary>
public class FastFlagsManager
{
    private readonly string _flagsFile;

    public FastFlagsManager(string? flagsFile = null)
    {
        _flagsFile = flagsFile ?? Path.Combine(KoroneConfig.AppDataDirectory, KoroneConfig.FastFlagsFileName);
    }

    /// <summary>Loads the local editing cache. Creates an empty one if missing.</summary>
    public Dictionary<string, object> Load()
    {
        if (!File.Exists(_flagsFile))
        {
            File.WriteAllText(_flagsFile, "{}");
            return new();
        }

        try
        {
            var json = File.ReadAllText(_flagsFile);
            if (string.IsNullOrWhiteSpace(json)) return new();

            using var doc = JsonDocument.Parse(json);
            return ParseFlags(doc);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[!] Error reading {_flagsFile}: {ex.Message}");
            return new();
        }
    }

    /// <summary>Persists the local editing cache (does NOT touch any installed client).</summary>
    public void Save(Dictionary<string, object> flags)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(flags, options);
        var tmp = _flagsFile + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_flagsFile))
            File.Copy(_flagsFile, _flagsFile + ".bak", overwrite: true);
        File.Move(tmp, _flagsFile, overwrite: true);
    }

    /// <summary>
    /// Writes <paramref name="flags"/> into every installed client's ClientAppSettings.json,
    /// backing up any existing file first. This is what actually makes FastFlags take effect.
    /// </summary>
    public ApplyResult ApplyToInstalledClients(Dictionary<string, object> flags)
    {
        var failures = new List<string>();
        var written = 0;

        foreach (var (clientDir, settingsFile, yearFolder) in VersionLocator.GetClientSettingsTargets())
        {
            try
            {
                Directory.CreateDirectory(clientDir);

                if (File.Exists(settingsFile))
                {
                    try { File.Copy(settingsFile, settingsFile + ".bak", overwrite: true); }
                    catch (Exception) { /* best-effort backup */ }
                }

                var json = JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFile, json);
                written++;
            }
            catch (Exception ex)
            {
                failures.Add($"{yearFolder}: {ex.Message}");
            }
        }

        return new ApplyResult(written, failures);
    }

    /// <summary>Reads back whatever is currently written into installed clients, for diagnostics.</summary>
    public IReadOnlyList<(string YearFolder, string SettingsFile, Dictionary<string, object>? Flags)> ReadAppliedFlags()
    {
        var results = new List<(string, string, Dictionary<string, object>?)>();

        foreach (var (_, settingsFile, yearFolder) in VersionLocator.GetClientSettingsTargets())
        {
            if (!File.Exists(settingsFile))
            {
                results.Add((yearFolder, settingsFile, null));
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(settingsFile));
                results.Add((yearFolder, settingsFile, ParseFlags(doc)));
            }
            catch (Exception)
            {
                results.Add((yearFolder, settingsFile, null));
            }
        }

        return results;
    }

    private static Dictionary<string, object> ParseFlags(JsonDocument doc)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var v = prop.Value;
            object normalized = v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when v.TryGetInt64(out var i) => i,
                JsonValueKind.Number when v.TryGetDouble(out var d) => d,
                JsonValueKind.String => v.GetString() ?? "",
                _ => v.ToString(),
            };
            result[prop.Name] = normalized;
        }
        return result;
    }

    public static object AutoDetectValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var trimmed = value.Trim();
        if (bool.TryParse(trimmed, out var b)) return b;
        if (int.TryParse(trimmed, out var i)) return i;
        if (double.TryParse(trimmed, out var d)) return d;
        return value;
    }
}
