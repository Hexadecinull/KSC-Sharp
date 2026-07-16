using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KSCSharp.Core.Platform;

/// <summary>
/// Looks up the main window handle (HWND) for a running client process, and can strip its
/// window chrome for a "fake" borderless fullscreen. This is real Win32 window manipulation
/// (SetWindowLong/SetWindowPos), not a FastFlag - Vulkan always forces exclusive fullscreen
/// regardless of client settings (confirmed via Bloxstrap's own FastFlags guide and multiple
/// user reports of the same behavior), so getting a borderless window out of it needs to
/// happen at the window level instead.
///
/// Windows-only for now: Linux (X11/Wayland) and macOS (Cocoa/AppKit) window manipulation use
/// completely different, much larger APIs, and would need to be built out separately rather
/// than bolted onto this the same way.
/// </summary>
public static class WindowManipulator
{
    private const int GWL_STYLE = -16;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_THICKFRAME = 0x00040000;
    private const uint WS_MINIMIZEBOX = 0x00020000;
    private const uint WS_MAXIMIZEBOX = 0x00010000;
    private const uint WS_SYSMENU = 0x00080000;

    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

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

    /// <summary>
    /// Strips the title bar/border and resizes the window to cover the full primary display -
    /// a "fake" borderless fullscreen, distinct from real exclusive fullscreen. Intended for
    /// Vulkan, which otherwise always forces exclusive fullscreen regardless of client
    /// settings. Returns false if anything about the window manipulation failed; the client
    /// keeps running either way, it just won't be resized.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static bool SetFakeBorderlessFullscreen(IntPtr hWnd)
    {
        if (!OperatingSystem.IsWindows() || hWnd == IntPtr.Zero)
            return false;

        try
        {
            var style = (uint)(Environment.Is64BitProcess ? (long)GetWindowLongPtr64(hWnd, GWL_STYLE) : GetWindowLong32(hWnd, GWL_STYLE));
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);

            if (Environment.Is64BitProcess)
                SetWindowLongPtr64(hWnd, GWL_STYLE, (IntPtr)style);
            else
                SetWindowLong32(hWnd, GWL_STYLE, (int)style);

            var width = GetSystemMetrics(SM_CXSCREEN);
            var height = GetSystemMetrics(SM_CYSCREEN);

            return SetWindowPos(hWnd, IntPtr.Zero, 0, 0, width, height, SWP_FRAMECHANGED | SWP_NOZORDER | SWP_NOACTIVATE);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
