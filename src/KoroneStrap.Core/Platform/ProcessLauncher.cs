using System.ComponentModel;
using System.Diagnostics;
using KSCSharp.Core.Models;

namespace KSCSharp.Core.Platform;

public class ProcessLaunchException : Exception
{
    public ProcessLaunchException(string message) : base(message) { }
    public ProcessLaunchException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Starts a Windows executable. On Windows this just runs it directly; on Linux/macOS it is
/// wrapped with wine64/wine (falling back between the two, matching koroneStrap's behavior),
/// with the same NVIDIA PRIME env vars koroneStrap sets on Linux.
/// </summary>
public static class ProcessLauncher
{
    /// <summary>
    /// Launches an executable, wrapping it with Wine if not on Windows.
    /// Throws <see cref="ProcessLaunchException"/> if the executable is missing or Wine can't be found.
    /// </summary>
    public static Process Launch(string exePath, IEnumerable<string>? args = null)
    {
        if (!File.Exists(exePath))
            throw new ProcessLaunchException($"Executable not found: {exePath}");

        var argList = (args ?? Enumerable.Empty<string>()).ToList();

        ProcessStartInfo startInfo;

        if (SystemInfo.IsWindows)
        {
            startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
            };
            foreach (var a in argList)
                startInfo.ArgumentList.Add(a);
        }
        else
        {
            var wineCommand = ResolveWineCommand()
                ?? throw new ProcessLaunchException(
                    "Wine is not installed. koroneStrap needs wine64 or wine on PATH to run Windows clients.");

            startInfo = new ProcessStartInfo
            {
                FileName = wineCommand,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(exePath);
            foreach (var a in argList)
                startInfo.ArgumentList.Add(a);

            if (SystemInfo.IsLinux)
            {
                foreach (var (key, value) in KoroneConfig.LinuxWineEnvironment)
                    startInfo.Environment[key] = value;
            }
        }

        try
        {
            var process = Process.Start(startInfo);
            return process ?? throw new ProcessLaunchException($"Failed to start process: {exePath}");
        }
        catch (Win32Exception ex)
        {
            throw new ProcessLaunchException($"Failed to launch {exePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Returns a runnable path to wine64/wine, or null if none can be found.
    ///
    /// This checks PATH first, then falls back to probing common install locations directly.
    /// That fallback matters more than it sounds: GUI apps on macOS (and Linux desktop-entry
    /// launches, to a lesser extent) don't inherit the full interactive-shell PATH, so
    /// Homebrew's /opt/homebrew/bin, MacPorts' /opt/local/bin, or CrossOver's bundled Wine
    /// are often invisible to a double-clicked app even though `which wine` works fine in
    /// Terminal. This is one of the more common reasons "Wine isn't found" on macOS even when
    /// it's actually installed.
    /// </summary>
    public static string? ResolveWineCommand()
    {
        foreach (var candidate in new[] { "wine64", "wine" })
        {
            if (IsCommandAvailable(candidate))
                return candidate;
        }

        foreach (var path in CandidateWinePaths())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static IEnumerable<string> CandidateWinePaths()
    {
        var names = new[] { "wine64", "wine" };
        var directories = new List<string>();

        if (SystemInfo.IsMacOS)
        {
            directories.AddRange(new[]
            {
                "/opt/homebrew/bin",                                                       // Homebrew, Apple Silicon
                "/usr/local/bin",                                                           // Homebrew, Intel
                "/opt/local/bin",                                                           // MacPorts
                "/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin",         // CrossOver
            });
        }
        else if (SystemInfo.IsLinux)
        {
            directories.AddRange(new[]
            {
                "/usr/bin",
                "/usr/local/bin",
                "/var/lib/flatpak/exports/bin",                                             // Flatpak (system)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "flatpak", "exports", "bin"),
            });
        }

        foreach (var dir in directories)
            foreach (var name in names)
                yield return Path.Combine(dir, name);
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                ArgumentList = { "--version" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (process is null)
                return false;

            process.WaitForExit(3000);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
