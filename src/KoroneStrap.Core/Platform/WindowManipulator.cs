using System.Runtime.Versioning;

namespace KSCSharp.Core.Platform;

/// <summary>
/// Looks up the main window handle (HWND) for a running client process, so future features
/// (always-on-top, custom title bar, forced resolution, etc.) have something to act on.
/// This is deliberately just the lookup - it doesn't change any window behavior yet.
///
/// Windows-only for now: Linux (X11/Wayland) and macOS (Cocoa/AppKit) window manipulation use
/// completely different, much larger APIs, and would need to be built out separately rather
/// than bolted onto this the same way.
/// </summary>
public static class WindowManipulator
{
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Returns the process's main window handle, waiting up to <paramref name="timeoutMs"/>
    /// for the window to appear (a freshly-launched process often hasn't created one yet).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static IntPtr? FindMainWindowHandle(int processId, int timeoutMs = 15000)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                    return process.MainWindowHandle;
            }
            catch (ArgumentException)
            {
                // process already exited
                return null;
            }

            Thread.Sleep(250);
        }

        return null;
    }
}
