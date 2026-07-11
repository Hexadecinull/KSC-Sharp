using System.Diagnostics;

namespace KSCSharp.Core.Platform;

public static class LinuxIntegration
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string LocalShare = Path.Combine(Home, ".local", "share");
    private static readonly string DesktopAppsDir = Path.Combine(LocalShare, "applications");
    private static readonly string IconsBase = Path.Combine(LocalShare, "icons", "hicolor");

    public static readonly string EntryFile = Path.Combine(DesktopAppsDir, KoroneConfig.LinuxDesktopFileName);
    public static readonly string UninstallEntryFile = Path.Combine(DesktopAppsDir, KoroneConfig.LinuxUninstallDesktopFileName);
    public static readonly string IconPath = Path.Combine(IconsBase, "96x96", "apps", KoroneConfig.LinuxIconFileName);

    /// <param name="launcherCommand">
    /// The exact command line used to re-invoke this app (e.g. the published executable path,
    /// or "dotnet /path/to/KSC-Sharp.App.dll" when run via `dotnet run`/framework-dependent).
    /// </param>
    public static void CreateDesktopEntry(string launcherCommand)
    {
        Directory.CreateDirectory(DesktopAppsDir);

        var desktopContent =
            $"[Desktop Entry]\n" +
            $"Name={KoroneConfig.ProductName}\n" +
            $"Exec={launcherCommand} --uri %u\n" +
            $"Type=Application\n" +
            $"Terminal=false\n" +
            $"MimeType=x-scheme-handler/{KoroneConfig.UriScheme}\n" +
            $"Categories=Game\n" +
            $"Icon={Path.GetFileNameWithoutExtension(KoroneConfig.LinuxIconFileName)}\n" +
            $"NoDisplay=true\n";
        File.WriteAllText(EntryFile, desktopContent);

        var uninstallContent =
            $"[Desktop Entry]\n" +
            $"Name=Uninstall {KoroneConfig.ProductName}\n" +
            $"Exec={launcherCommand} --uninstall\n" +
            $"Type=Application\n" +
            $"Terminal=true\n" +
            $"Categories=Game\n" +
            $"Icon={Path.GetFileNameWithoutExtension(KoroneConfig.LinuxIconFileName)}\n";
        File.WriteAllText(UninstallEntryFile, uninstallContent);
    }

    public static void RegisterMimeHandler()
    {
        RunBestEffort("update-desktop-database", DesktopAppsDir);
        RunBestEffort("xdg-mime", $"default {Path.GetFileName(EntryFile)} x-scheme-handler/{KoroneConfig.UriScheme}");
    }

    /// <summary>Writes the bundled app icon to the Linux icon theme directory. No network access needed.</summary>
    public static void InstallIcon()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(IconPath)!);

        using var resourceStream = typeof(LinuxIntegration).Assembly
            .GetManifestResourceStream("KSCSharp.Core.Assets.icon.png");

        if (resourceStream is null)
            throw new InvalidOperationException("Bundled icon resource not found.");

        using var fileStream = new FileStream(IconPath, FileMode.Create, FileAccess.Write);
        resourceStream.CopyTo(fileStream);
    }

    public static void UninstallIntegration()
    {
        TryDelete(EntryFile);
        TryDelete(UninstallEntryFile);
        TryDelete(IconPath);
        RunBestEffort("update-desktop-database", DesktopAppsDir);
    }

    /// <summary>Best-effort status snapshot, mirrors koroneStrap's `debug` command for Linux integration.</summary>
    public static (bool DesktopEntryExists, bool UninstallEntryExists, bool IconExists) GetStatus()
        => (File.Exists(EntryFile), File.Exists(UninstallEntryFile), File.Exists(IconPath));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception)
        {
            // best-effort cleanup
        }
    }

    private static void RunBestEffort(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process?.WaitForExit(5000);
        }
        catch (Exception)
        {
            // tool may not be installed - not fatal
        }
    }
}
