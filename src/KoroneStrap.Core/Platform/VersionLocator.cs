using KSCSharp.Core.Models;

namespace KSCSharp.Core.Platform;

/// <summary>
/// Locates installed clients on disk.
///
/// Install layout (matches upstream koroneStrap, and real Korone (Pekora/ProjectX) installs):
///   &lt;VersionsRoot&gt;/&lt;version-hash-dir&gt;/&lt;yearFolder&gt;/ProjectXPlayerBeta.exe
///
/// On Windows, VersionsRoot is under %LocalAppData%. On Linux/macOS the client is a Windows
/// binary, so VersionsRoot lives inside a Wine prefix (or a CrossOver bottle on macOS).
/// </summary>
public static class VersionLocator
{
    /// <summary>Candidate "Versions" root directories for the current platform. Not all need to exist.</summary>
    public static IEnumerable<string> GetVersionRoots()
    {
        if (SystemInfo.IsWindows)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (var folder in KoroneConfig.InstallFolderNames)
                yield return Path.Combine(localAppData, folder, "Versions");

            yield break;
        }

        var user = Environment.UserName;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Default Wine prefix (~/.wine)
        foreach (var folder in KoroneConfig.InstallFolderNames)
            yield return Path.Combine(home, ".wine", "drive_c", "users", user, "AppData", "Local", folder, "Versions");

        // Dedicated per-app Wine prefixes some install scripts create
        foreach (var folder in KoroneConfig.InstallFolderNames)
            yield return Path.Combine(home, ".local", "share", "wineprefixes", folder.ToLowerInvariant(), "drive_c", "users", user, "AppData", "Local", folder, "Versions");

        if (SystemInfo.IsMacOS)
        {
            var bottlesRoot = Path.Combine(home, "Library", "Application Support", "CrossOver", "Bottles");
            if (Directory.Exists(bottlesRoot))
            {
                foreach (var bottle in SafeGetDirectories(bottlesRoot))
                {
                    foreach (var folder in KoroneConfig.InstallFolderNames)
                        yield return Path.Combine(bottle, "drive_c", "users", user, "AppData", "Local", folder, "Versions");
                }
            }
        }
    }

    /// <summary>Every per-install directory found under any version root (e.g. the "version-xxxx" hash folders).</summary>
    public static IEnumerable<string> GetInstalledVersionDirs()
    {
        foreach (var root in GetVersionRoots())
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var dir in SafeGetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                yield return dir;
        }
    }

    /// <summary>All plausible executable paths for a given year folder (e.g. "2020L"), across every install found.</summary>
    public static IEnumerable<string> GetExecutablePaths(string yearFolder)
        => GetInstalledVersionDirs().Select(v => Path.Combine(v, yearFolder, KoroneConfig.ClientExecutableName));

    /// <summary>Resolves the first executable that actually exists on disk for a year folder, if any.</summary>
    public static string? FindExecutable(string yearFolder)
        => GetExecutablePaths(yearFolder).FirstOrDefault(File.Exists);

    /// <summary>
    /// ClientSettings targets (one per installed client year folder found on disk) that FastFlags
    /// get written into. Mirrors koroneStrap's get_clientsettings_targets().
    /// </summary>
    public static IEnumerable<(string ClientSettingsDir, string SettingsFile, string YearFolder)> GetClientSettingsTargets()
    {
        foreach (var versionDir in GetInstalledVersionDirs())
        {
            foreach (var clientVersion in KoroneConfig.ClientVersions)
            {
                var yearFolderPath = Path.Combine(versionDir, clientVersion.FolderName);
                if (!Directory.Exists(yearFolderPath))
                    continue;

                var clientSettingsDir = Path.Combine(yearFolderPath, KoroneConfig.ClientSettingsFolderName);
                var settingsFile = Path.Combine(clientSettingsDir, KoroneConfig.ClientAppSettingsFileName);
                yield return (clientSettingsDir, settingsFile, clientVersion.FolderName);
            }
        }
    }

    private static IEnumerable<string> SafeGetDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (Exception)
        {
            return Enumerable.Empty<string>();
        }
    }
}
