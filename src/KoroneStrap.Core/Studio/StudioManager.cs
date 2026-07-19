using System.Net.Http;
using System.IO.Compression;
using KSCSharp.Core.Models;
using KSCSharp.Core.Platform;

namespace KSCSharp.Core.Studio;

public record StudioScanResult(string Year, string Path);

public static class StudioManager
{
    /// <summary>
    /// Scans available drives for any of KoroneConfig.StudioExecutableCandidates, in parallel
    /// across subdirectories, up to a bounded depth so this can't turn into an unbounded
    /// all-night crawl. Call this ONLY after explicit user consent - it walks the selected
    /// drive(s) and can still take a while on a large one, though far less than a naive
    /// single-threaded walk.
    ///
    /// Speed comes from three things, in order of impact:
    ///  1. Parallel directory enumeration (bounded concurrency) instead of strictly serial
    ///     depth-first recursion - directory listing is I/O-bound, so multiple concurrent
    ///     requests generally finish far faster than one at a time, especially on SSDs.
    ///  2. Aggressive, specific pruning of directories that are large, irrelevant, and/or
    ///     pathologically slow to enumerate - cloud-sync placeholder folders (OneDrive,
    ///     Dropbox, Google Drive) are the standout case: each file in them can trigger a
    ///     network round-trip just to answer "does this file exist," which can turn a
    ///     10-second local scan into a 10-minute one on a drive with a large synced folder.
    ///  3. progress lets the caller show live "still working, currently in X" status instead
    ///     of an indefinite spinner with no feedback - a real UX bug on its own even when the
    ///     scan itself is fast, since "is this hung or just slow" was previously unanswerable.
    /// </summary>
    public static async Task<List<StudioScanResult>> ScanDrivesForStudioAsync(
        IProgress<string>? progress = null,
        int maxDepth = 6,
        int maxConcurrency = 8,
        CancellationToken ct = default)
    {
        var results = new List<StudioScanResult>();
        var resultsLock = new object();
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        async Task ScanDirAsync(string dir, int depthRemaining)
        {
            if (depthRemaining <= 0 || ct.IsCancellationRequested)
                return;

            var dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (IsNoiseDirectory(dirName))
                return;

            progress?.Report(dir);

            foreach (var candidate in KoroneConfig.StudioExecutableCandidates)
            {
                string exePath;
                try { exePath = Path.Combine(dir, candidate); }
                catch (Exception) { return; }

                if (File.Exists(exePath))
                {
                    var year = InferYearFromPath(dir);
                    if (year is not null)
                    {
                        lock (resultsLock)
                            results.Add(new StudioScanResult(year, dir));
                    }
                    break; // found one candidate in this dir, no need to check the others
                }
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch (Exception)
            {
                return; // no permission, or the directory vanished mid-scan - just skip it
            }

            var tasks = new List<Task>(subdirs.Length);
            foreach (var sub in subdirs)
            {
                await semaphore.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try { await ScanDirAsync(sub, depthRemaining - 1); }
                    finally { semaphore.Release(); }
                }, ct));
            }

            await Task.WhenAll(tasks);
        }

        var driveTasks = SafeGetDrives()
            .Select(drive => ScanDirAsync(drive.RootDirectory.FullName, maxDepth))
            .ToList();

        await Task.WhenAll(driveTasks);
        return results;
    }

    private static string? InferYearFromPath(string path)
    {
        foreach (var year in KoroneConfig.StudioDownloadUrls.Keys)
        {
            if (path.Contains(year, StringComparison.OrdinalIgnoreCase))
                return year;
        }
        return null;
    }

    private static readonly HashSet<string> NoiseDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows/system noise - large, irrelevant, sometimes access-restricted
        "$Recycle.Bin", "System Volume Information", "Windows", "ProgramData",
        "Program Files", "Program Files (x86)", "Windows.old", "Recovery",
        "PerfLogs", "config.msi",

        // Dev tooling noise - large, irrelevant
        "node_modules", ".git", ".svn", ".hg", "proc", "sys", "dev",

        // Cloud-sync placeholder folders - each entry can trigger a slow network round-trip
        // to resolve, turning what should be a fast local scan into a very slow one. Portable
        // Studio installs living inside one of these would still get missed by this pruning,
        // but scanning through them at all-file granularity was the single biggest known
        // cause of multi-minute scans.
        "OneDrive", "Dropbox", "Google Drive", "iCloudDrive", "iCloud Drive", "Box", "pCloudDrive",

        // Package manager / game-platform noise - huge, essentially never relevant here
        "steamapps", "Epic Games", "WindowsApps", "AppData",
    };

    private static bool IsNoiseDirectory(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return NoiseDirectoryNames.Contains(name) || name.StartsWith('.');
    }

    private static IEnumerable<DriveInfo> SafeGetDrives()
    {
        try
        {
            return DriveInfo.GetDrives().Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable));
        }
        catch (Exception)
        {
            return Enumerable.Empty<DriveInfo>();
        }
    }

    /// <summary>
    /// A lightweight "has this changed" signal from the server without downloading the whole
    /// archive - ETag first, then Last-Modified, then Content-Length as fallbacks. This is
    /// what actually gets compared as the "hash" in the UI; a true content hash would mean
    /// downloading the whole archive just to check for updates, which isn't a good trade for a
    /// multi-hundred-MB download.
    /// </summary>
    public static async Task<string?> GetRemoteFingerprintAsync(string year, HttpClient? httpClient = null)
    {
        if (!KoroneConfig.StudioDownloadUrls.TryGetValue(year, out var url))
            return null;

        var http = httpClient ?? new HttpClient();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            if (response.Headers.ETag is { } etag)
                return etag.Tag;
            if (response.Content.Headers.LastModified is { } modified)
                return modified.ToString();
            if (response.Content.Headers.ContentLength is { } length)
                return length.ToString();

            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (httpClient is null)
                http.Dispose();
        }
    }

    public static async Task<bool> DownloadAndInstallAsync(
        string year,
        string targetDir,
        IProgress<(long downloaded, long? total)>? progress = null,
        CancellationToken ct = default)
    {
        if (!KoroneConfig.StudioDownloadUrls.TryGetValue(year, out var url))
            return false;

        var tempZip = Path.Combine(Path.GetTempPath(), $"korone-studio-{year}-{Guid.NewGuid():N}.zip");

        try
        {
            using var http = new HttpClient();
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength;

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, ct)) != 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;
                    progress?.Report((totalRead, total));
                }
            }

            Directory.CreateDirectory(targetDir);
            ZipFile.ExtractToDirectory(tempZip, targetDir, overwriteFiles: true);

            var fingerprint = await GetRemoteFingerprintAsync(year);
            var settings = StudioSettings.Load();
            var info = settings.GetOrCreate(year);
            info.Path = targetDir;
            info.LastKnownRemoteFingerprint = fingerprint;
            settings.Save();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); }
            catch (Exception) { /* best-effort cleanup */ }
        }
    }

    /// <summary>Finds whichever candidate executable actually exists in an install directory.</summary>
    public static string? FindStudioExecutable(string installPath)
    {
        foreach (var candidate in KoroneConfig.StudioExecutableCandidates)
        {
            var path = Path.Combine(installPath, candidate);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    public static void Launch(string installPath)
    {
        var exePath = FindStudioExecutable(installPath)
            ?? Path.Combine(installPath, KoroneConfig.StudioExecutableCandidates[0]);
        ProcessLauncher.Launch(exePath);
    }
}
