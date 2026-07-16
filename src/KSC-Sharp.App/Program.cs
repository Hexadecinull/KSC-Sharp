using Avalonia;
using KSCSharp.Core;
using KSCSharp.Core.Models;
using KSCSharp.Core.Platform;
using System.IO;

namespace KSCSharp.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && IsInstallOrUninstall(args[0]))
        {
            var handled = TryHandleCliArgs(args, out var exitCode);
            if (handled)
                return exitCode;
        }

        if (args.Length > 0 && TryGetRawUri(args, out var rawUri))
        {
            // Routed through the normal Avalonia startup (not handled headlessly) so it can
            // show a loading window - see App.axaml.cs. Intentionally skips the single-instance
            // lock below: a join-link click should always get its own loading window, even if
            // the main GUI is already open.
            PendingUri = rawUri;
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }

        var singleInstanceLock = AcquireSingleInstanceLock();
        if (singleInstanceLock is null)
        {
            Console.Error.WriteLine("[*] KSC-Sharp is already running.");
            return 0;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            singleInstanceLock.Dispose();
        }

        return 0;
    }

    /// <summary>Set by Main() before starting Avalonia when launched via a join link; read by App.axaml.cs.</summary>
    public static string? PendingUri { get; private set; }

    private static bool IsInstallOrUninstall(string first) =>
        first is "--install" or "--uninstall" or "-u";

    /// <summary>
    /// Prevents launching a second GUI window (e.g. double-clicking the icon while it's
    /// already open). Holds an exclusive lock on a file in AppData for the process lifetime;
    /// the OS releases it automatically on exit, including a crash. Headless flows
    /// (--uri/--install/--uninstall) intentionally skip this - they're fire-and-forget and
    /// shouldn't be blocked by an already-open GUI instance.
    /// </summary>
    private static FileStream? AcquireSingleInstanceLock()
    {
        var lockPath = Path.Combine(KoroneConfig.AppDataDirectory, ".instance.lock");
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>Detects a pekora-player:// URI as either argv[0] directly, or as `--uri &lt;value&gt;`.</summary>
    private static bool TryGetRawUri(string[] args, out string rawUri)
    {
        var first = args[0];

        if (first.StartsWith($"{KoroneConfig.UriScheme}://", StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith($"{KoroneConfig.UriScheme}:", StringComparison.OrdinalIgnoreCase))
        {
            rawUri = first;
            return true;
        }

        if (first == "--uri" && args.Length > 1)
        {
            rawUri = args[1];
            return true;
        }

        rawUri = "";
        return false;
    }

    /// <summary>Returns true if a headless CLI flow was matched (and the app should exit without a GUI).</summary>
    private static bool TryHandleCliArgs(string[] args, out int exitCode)
    {
        switch (args[0])
        {
            case "--install":
                exitCode = HandleInstall();
                return true;

            case "--uninstall":
            case "-u":
                exitCode = HandleUninstall();
                return true;

            default:
                exitCode = 0;
                return false;
        }
    }

    private static int HandleInstall()
    {
        if (SystemInfo.IsLinux)
        {
            var launcherCommand = ResolveLauncherCommand();
            LinuxIntegration.CreateDesktopEntry(launcherCommand);
            LinuxIntegration.InstallIcon();
            LinuxIntegration.RegisterMimeHandler();
            Console.WriteLine("[*] Linux integration setup complete!");
            return 0;
        }

        if (SystemInfo.IsWindows)
        {
            var (success, message) = WindowsUriRegistration.Register();
            Console.WriteLine(success ? $"[*] {message}" : $"[!] {message}");
            return success ? 0 : 1;
        }

        Console.WriteLine("[!] --install has no effect on this platform yet.");
        return 0;
    }

    private static int HandleUninstall()
    {
        if (SystemInfo.IsLinux)
        {
            LinuxIntegration.UninstallIntegration();
            Console.WriteLine("[*] Linux integration uninstalled!");
            PurgeAppData();
            return 0;
        }

        if (SystemInfo.IsWindows)
        {
            var (success, message) = WindowsUriRegistration.Unregister();
            Console.WriteLine(success ? $"[*] {message}" : $"[!] {message}");
            PurgeAppData();
            return success ? 0 : 1;
        }

        Console.WriteLine("[!] --uninstall has no effect on this platform yet.");
        PurgeAppData();
        return 0;
    }

    /// <summary>
    /// Removes the downloaded bootstrapper and local FastFlags cache. Called by --uninstall
    /// (and wired into the Inno Setup [UninstallRun] step) so removing the app doesn't leave
    /// files behind in the per-user data directory - the app's own install folder never held
    /// this data in the first place (see KoroneConfig.AppDataDirectory).
    /// </summary>
    private static void PurgeAppData()
    {
        try
        {
            var dir = KoroneConfig.AppDataDirectory;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
                Console.WriteLine($"[*] Removed {dir}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[!] Could not fully remove app data: {ex.Message}");
        }
    }

    private static string ResolveLauncherCommand()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) && !processPath.EndsWith("dotnet", StringComparison.OrdinalIgnoreCase))
            return processPath;

        // Running via `dotnet run`/`dotnet KSC-Sharp.App.dll` - point Exec= at the dll instead.
        var dllPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        return !string.IsNullOrEmpty(dllPath) ? $"dotnet {dllPath}" : "KSC-Sharp.App";
    }
}
