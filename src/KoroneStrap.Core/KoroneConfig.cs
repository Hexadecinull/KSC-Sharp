using KSCSharp.Core.Models;

namespace KSCSharp.Core;

/// <summary>
/// Every product-specific constant (branding, URLs, file/folder names) lives here.
/// koroneStrap (the upstream Python project) changes these fairly often - when it does,
/// this should be the only file that needs touching.
///
/// A note on "Pekora" vs "Korone": Korone is the current name of the platform; Pekora is
/// its old name. Display text/branding in this codebase uses "Korone" throughout. What does
/// NOT change here are values that reflect the platform's actual, real technical identity -
/// the registered URI scheme (pekora-player://), the download domain (pekora.zip), and the
/// bootstrapper's real filename - because those are what the live platform and its users'
/// browsers actually use today. Renaming those would silently break real interop rather than
/// just being a cosmetic update.
/// </summary>
public static class KoroneConfig
{
    public const string ProductName = "Korone";
    public const string AppName = "KSC-Sharp";

    /// <summary>Custom URI scheme used for protocol launches, e.g. pekora-player://launchmode:...</summary>
    public const string UriScheme = "pekora-player";

    /// <summary>Executable name inside each installed client's year folder.</summary>
    public const string ClientExecutableName = "ProjectXPlayerBeta.exe";

    /// <summary>Folder names under LocalAppData / the Wine prefix that may contain a "Versions" directory.</summary>
    public static readonly string[] InstallFolderNames = { "ProjectX", "Pekora" };

    public const string BootstrapperUrl = "https://setup.pekora.zip/PekoraPlayerLauncher.exe";
    public const string BootstrapperFileName = "PekoraPlayerLauncher.exe";
    public const string NegotiateUrl = "https://www.pekora.zip/Login/Negotiate.ashx";

    public const string LinuxDesktopFileName = "korone-player.desktop";
    public const string LinuxUninstallDesktopFileName = "uninstall-korone-player.desktop";
    public const string LinuxIconFileName = "korone-player.png";

    public const string FastFlagsFileName = "fastFlags.json";
    public const string ClientSettingsFolderName = "ClientSettings";
    public const string ClientAppSettingsFileName = "ClientAppSettings.json";

    /// <summary>
    /// Legacy client years selectable from the UI. All four go through the exact same
    /// VersionLocator/ProcessLauncher/FastFlagsManager path - there's no separate launch
    /// mechanism for older years, so there's nothing year-specific to "fix" in this codebase.
    /// Runtime reliability of any given build (especially older DirectX 9-era clients under
    /// Wine) is outside what this launcher controls.
    /// </summary>
    public static readonly IReadOnlyList<ClientVersion> ClientVersions = new[]
    {
        new ClientVersion("2017", "2017L", Available: true),
        new ClientVersion("2018", "2018",  Available: true),
        new ClientVersion("2020", "2020L", Available: true),
        new ClientVersion("2021", "2021M", Available: true),
    };

    /// <summary>
    /// Korone Studio download URLs, one independent portable ZIP per year - unlike the Player,
    /// these aren't confirmed to share the Versions/hash-folder install layout, so each is
    /// tracked and updated separately. Confirmed live (fetched and got a real, large response)
    /// as of this writing, but the internal folder/executable layout inside each ZIP has not
    /// been inspected - see StudioManager's notes.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> StudioDownloadUrls = new Dictionary<string, string>
    {
        ["2017"] = "https://setup.pekora.zip/PekoraStudio2017.zip",
        ["2018"] = "https://setup.pekora.zip/PekoraStudio2018.zip",
        ["2020"] = "https://setup.pekora.zip/PekoraStudio2020.zip",
        ["2021"] = "https://setup.pekora.zip/PekoraStudio2021.zip",
    };

    /// <summary>
    /// Best-effort guess at the Studio executable name inside each extracted ZIP, inferred
    /// from this project's own ClientExecutableName pattern (ProjectX + role + Beta) and
    /// Bloxstrap's equivalent (RobloxPlayerBeta.exe / RobloxStudioBeta.exe use the same
    /// Player/Studio naming split). NOT verified against a real extracted install - the ZIPs
    /// are too large to fetch and inspect from here. If this is wrong, StudioManager's drive
    /// scan just won't find anything, and this is the one constant to fix.
    /// </summary>
    public const string StudioExecutableName = "ProjectXStudioBeta.exe";

    /// <summary>
    /// Per-user directory for files this app generates at runtime (downloaded bootstrapper,
    /// local FastFlags cache) - deliberately NOT the app's own install directory, which may
    /// be a read-only per-machine location like Program Files.
    /// </summary>
    public static string AppDataDirectory
    {
        get
        {
            string baseDir;
            if (Models.SystemInfo.IsMacOS)
            {
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
            }
            else if (Models.SystemInfo.IsLinux)
            {
                var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                baseDir = !string.IsNullOrEmpty(xdgData)
                    ? xdgData
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            }
            else
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            var dir = Path.Combine(baseDir, AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Environment variables koroneStrap sets before launching under Wine on Linux.</summary>
    public static readonly IReadOnlyDictionary<string, string> LinuxWineEnvironment = new Dictionary<string, string>
    {
        ["__NV_PRIME_RENDER_OFFLOAD"] = "1",
        ["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia",
    };

    /// <summary>
    /// Discord Application ID for Rich Presence. THIS IS A PLACEHOLDER - Rich Presence will
    /// not work until this is replaced with a real application ID from
    /// https://discord.com/developers/applications (create an app, copy its "Application ID").
    /// </summary>
    public const string DiscordClientId = "0000000000000000000";

    /// <summary>
    /// Rich Presence "Art Asset" key. Needs to be uploaded under the Discord application's
    /// Rich Presence > Art Assets page with this exact name before it'll actually show an image.
    /// </summary>
    public const string DiscordLargeImageKey = "logo";
    public const string DiscordLargeImageText = ProductName;
}
