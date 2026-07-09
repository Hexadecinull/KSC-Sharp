using System.Runtime.InteropServices;

namespace KSCSharp.Core.Models;

public static class SystemInfo
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static string SystemName =>
        IsWindows ? "windows" : (IsLinux ? "linux" : (IsMacOS ? "darwin" : RuntimeInformation.OSDescription.ToLowerInvariant()));

    /// <summary>True on Linux/macOS, where the client is a Windows .exe run through Wine.</summary>
    public static bool RequiresWine => IsLinux || IsMacOS;
}
