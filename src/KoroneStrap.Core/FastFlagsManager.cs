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
///    which is what Korone/Roblox itself reads at startup. Writing only the former (which is
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
    ///
    /// Respects AppSettings.FastFlagsManagementEnabled - if the user has turned off "Allow
    /// KSC-Sharp to manage Fast Flags", this is a no-op regardless of what called it. That
    /// check lives here (not just in the UI) so it's enforced everywhere, including the
    /// headless --uri launch path in Program.cs.
    /// </summary>
    public ApplyResult ApplyToInstalledClients(Dictionary<string, object> flags)
    {
        if (!AppSettings.Load().FastFlagsManagementEnabled)
            return new ApplyResult(0, new[] { "FastFlags management is disabled (Integrations > FastFlags)." });

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

    /// <summary>
    /// Clears the local FastFlags cache and, if management is enabled, writes an empty flags
    /// object into every installed client too - a real reset, not just forgetting what was
    /// configured locally while leaving old flags active in the client.
    /// </summary>
    public ApplyResult ResetAll()
    {
        Save(new Dictionary<string, object>());
        return ApplyToInstalledClients(new Dictionary<string, object>());
    }

    /// <summary>
    /// The generic FastFlags cache, overlaid with the structured Global Settings > Presets
    /// (Graphics API, Framerate Limit). This is what should actually be applied to a client -
    /// the generic editor and the structured presets both end up in the same
    /// ClientAppSettings.json, since that's the only place either one can take effect.
    /// </summary>
    public Dictionary<string, object> BuildEffectiveFlags(AppSettings settings)
    {
        var flags = Load();
        EngineFlags.ApplyGraphicsApi(flags, settings.GraphicsApi);
        flags[EngineFlags.TaskSchedulerTargetFps] = settings.FramerateLimit;
        EngineFlags.ApplyMeshDetail(flags, settings.MeshDetailReduced);
        return flags;
    }

    /// <summary>
    /// Reads back what's actually in each installed client's ClientAppSettings.json and
    /// confirms the Graphics API preset matches what was meant to be applied - a write can
    /// "succeed" (no exception) without necessarily sticking, e.g. a permissions issue or a
    /// concurrent write from the client itself. Returns install years where it doesn't match.
    /// </summary>
    public List<string> VerifyGraphicsApiApplied(GraphicsApi expected)
    {
        var mismatches = new List<string>();

        foreach (var (year, _, applied) in ReadAppliedFlags())
        {
            if (applied is null)
            {
                mismatches.Add($"{year}: no ClientAppSettings.json found to verify");
                continue;
            }

            var expectedFlags = new Dictionary<string, object>();
            EngineFlags.ApplyGraphicsApi(expectedFlags, expected);

            foreach (var (key, value) in expectedFlags)
            {
                if (!applied.TryGetValue(key, out var actual) || !Equals(NormalizeBool(actual), NormalizeBool(value)))
                {
                    mismatches.Add($"{year}: expected {key}={value}, found {(applied.TryGetValue(key, out var v) ? v : "(missing)")}");
                }
            }
        }

        return mismatches;
    }

    private static object NormalizeBool(object value) =>
        value is bool b ? b : (value?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false);

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
