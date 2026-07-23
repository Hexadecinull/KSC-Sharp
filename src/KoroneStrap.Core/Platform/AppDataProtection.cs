using System.Runtime.Versioning;
using System.Security.AccessControl;

namespace KSCSharp.Core.Platform;

/// <summary>
/// Restricts KSC-Sharp's AppData directory (and everything in it - settings.json, the local
/// FastFlags cache, studio.json) to the current user only.
///
/// This is defense-in-depth, not the primary protection: %LocalAppData% on Windows and
/// ~/.local/share on Linux/macOS are already created user-private by the OS in the overwhelming
/// majority of real installs, so this mainly guards against unusual configurations (a shared
/// machine with loose umask settings, a misconfigured Windows profile, etc). Every operation
/// here is best-effort and non-fatal - a failure just means the OS default protection is all
/// that's in place, not that anything breaks.
///
/// Nothing especially sensitive lives in this directory today (no passwords, no session
/// cookies, no auth tickets - see the README's Data &amp; Privacy section for what's
/// deliberately never read or stored), but hardening it now costs nothing and matters more as
/// soon as anything more sensitive is added later.
/// </summary>
public static class AppDataProtection
{
    private static bool _applied;

    /// <summary>Idempotent - safe to call on every AppDataDirectory access; only does real work once per process.</summary>
    public static void EnsureHardened(string directoryPath)
    {
        if (_applied)
            return;
        _applied = true;

        try
        {
            if (OperatingSystem.IsWindows())
                HardenWindows(directoryPath);
            else
                HardenUnix(directoryPath);
        }
        catch (Exception)
        {
            // best-effort - never let this block the app from starting
        }
    }

    [SupportedOSPlatform("windows")]
    private static void HardenWindows(string directoryPath)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var info = new DirectoryInfo(directoryPath);
            var security = info.GetAccessControl();

            // Strip inherited rules and grant access only to the current user + built-in
            // Administrators (so an admin can still manage/repair the machine), removing
            // broader groups like "Users" or "Everyone" that inheritance from the parent
            // folder might otherwise carry in.
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().User;
            if (currentUser is not null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    currentUser, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
            }

            var admins = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                admins, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));

            info.SetAccessControl(security);
        }
        catch (Exception)
        {
            // best-effort - e.g. insufficient privilege to change ACLs on this volume
        }
    }

    private static void HardenUnix(string directoryPath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            // rwx for owner only (0700) on the directory itself.
            File.SetUnixFileMode(directoryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            // rw for owner only (0600) on every file already inside it - covers upgrades from
            // a version of KSC-Sharp that predates this hardening and left files world-readable.
            foreach (var file in Directory.EnumerateFiles(directoryPath))
            {
                try
                {
                    File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (Exception)
                {
                    // best-effort per-file - keep going
                }
            }
        }
        catch (Exception)
        {
            // best-effort - e.g. filesystem doesn't support Unix permission bits
        }
    }
}
