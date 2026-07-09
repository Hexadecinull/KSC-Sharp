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

    /// <summary>Returns "wine64" or "wine" (preferring wine64), or null if neither is on PATH.</summary>
    public static string? ResolveWineCommand()
    {
        foreach (var candidate in new[] { "wine64", "wine" })
        {
            if (IsCommandAvailable(candidate))
                return candidate;
        }

        return null;
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
