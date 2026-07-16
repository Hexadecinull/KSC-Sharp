using KSCSharp.Core.Models;
using Microsoft.Win32;

namespace KSCSharp.Core.Platform;

/// <summary>
/// Registers/unregisters the pekora-player:// URI scheme in the current user's registry hive.
///
/// Note on why there's no #if here: Microsoft.Win32.Registry types are part of the base
/// class library reference assemblies for a plain "net8.0" TFM (they're annotated
/// [SupportedOSPlatform("windows")] and throw PlatformNotSupportedException off-Windows at
/// runtime). That means this compiles fine on every OS; guarding with OperatingSystem.IsWindows()
/// at the call site is both necessary and sufficient - no "net8.0-windows" TFM or preprocessor
/// symbol is required. The previous code used #if NET8_0_WINDOWS, which is only ever defined
/// when TargetFramework is literally "net8.0-windows", which this project never set, so that
/// branch was dead on every platform including Windows.
/// </summary>
public static class WindowsUriRegistration
{
    public static (bool Success, string Message) Register(string? exePathOverride = null)
    {
        if (!OperatingSystem.IsWindows())
            return (false, "URI scheme registration is only supported on Windows.");

        try
        {
            var exePath = exePathOverride
                ?? Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? $"{KoroneConfig.AppName}.exe";

            var commandValue = $"\"{exePath}\" --uri \"%1\"";

            using (var baseKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{KoroneConfig.UriScheme}"))
            {
                baseKey?.SetValue("", $"URL:{KoroneConfig.ProductName} Protocol", RegistryValueKind.String);
                baseKey?.SetValue("URL Protocol", "", RegistryValueKind.String);
            }

            using (var shellKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{KoroneConfig.UriScheme}\shell\open\command"))
            {
                shellKey?.SetValue("", commandValue, RegistryValueKind.String);
            }

            // Read back what was actually written rather than assuming SetValue silently
            // succeeded - CreateSubKey/SetValue can return without throwing even when the
            // value didn't end up as expected (e.g. redirected/virtualized registry views).
            using var verifyKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{KoroneConfig.UriScheme}\shell\open\command");
            var written = verifyKey?.GetValue("") as string;
            if (!string.Equals(written, commandValue, StringComparison.Ordinal))
                return (false, "URI scheme registration didn't verify - the registry value wasn't what was written. Try running as the same user that will open links, and check for a Group Policy blocking HKCU\\Software\\Classes writes.");

            return (true, $"Registered URI scheme: {KoroneConfig.UriScheme}://");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to register URI scheme: {ex.Message}");
        }
    }

    public static (bool Success, string Message) Unregister()
    {
        if (!OperatingSystem.IsWindows())
            return (false, "URI scheme unregistration is only supported on Windows.");

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{KoroneConfig.UriScheme}", throwOnMissingSubKey: false);
            return (true, $"Unregistered URI scheme: {KoroneConfig.UriScheme}://");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to unregister URI scheme: {ex.Message}");
        }
    }
}
