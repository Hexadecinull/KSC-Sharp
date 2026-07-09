using KSCSharp.Core.Models;

namespace KSCSharp.Core;

/// <summary>
/// Every product-specific constant (branding, URLs, file/folder names) lives here.
/// koroneStrap (the upstream Python project) changes these fairly often — when it does,
/// this should be the only file that needs touching.
/// </summary>
public static class KoroneConfig
{
    public const string ProductName = "Pekora Player";
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

    public const string LinuxIconUrl = "https://raw.githubusercontent.com/johnhamilcar/PekoraBootstrapperLinux/refs/heads/main/pekora-player-bootstrapper.png";
    public const string LinuxDesktopFileName = "pekora-player.desktop";
    public const string LinuxUninstallDesktopFileName = "uninstall-pekora-player.desktop";
    public const string LinuxIconFileName = "pekora-player.png";

    public const string FastFlagsFileName = "fastFlags.json";
    public const string ClientSettingsFolderName = "ClientSettings";
    public const string ClientAppSettingsFileName = "ClientAppSettings.json";

    /// <summary>
    /// Legacy client years selectable from the UI. "Available" mirrors what upstream currently
    /// ships working support for — 2017/2018 are listed upstream but are still WIP there too.
    /// </summary>
    public static readonly IReadOnlyList<ClientVersion> ClientVersions = new[]
    {
        new ClientVersion("2017", "2017L", Available: false),
        new ClientVersion("2018", "2018",  Available: false),
        new ClientVersion("2020", "2020L", Available: true),
        new ClientVersion("2021", "2021M", Available: true),
    };

    /// <summary>Environment variables koroneStrap sets before launching under Wine on Linux.</summary>
    public static readonly IReadOnlyDictionary<string, string> LinuxWineEnvironment = new Dictionary<string, string>
    {
        ["__NV_PRIME_RENDER_OFFLOAD"] = "1",
        ["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia",
    };
}
