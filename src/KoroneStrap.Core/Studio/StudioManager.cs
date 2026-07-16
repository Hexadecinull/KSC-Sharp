using System.Net.Http;
using System.IO.Compression;
using KSCSharp.Core.Models;
using KSCSharp.Core.Platform;

namespace KSCSharp.Core.Studio;

public record StudioScanResult(string Year, string Path);

public static class StudioManager
{
    /// <summary>
    /// Scans available drives for KoroneConfig.StudioExecutableName, up to a bounded depth so
    /// this can't turn into an unbounded, all-night filesystem crawl. Call this ONLY after
    /// explicit user consent - it walks the entire selected drive(s) and can take a while.
    /// Known-noisy system directories are skipped, and any access error just skips that
    /// branch rather than aborting the whole scan.
    /// </summary>
    public static IEnumerable<StudioScanResult> ScanDrivesForStudio(int maxDepth = 7, CancellationToken ct = default)
    {
        foreach (var drive in SafeGetDrives())
        {
            ct.ThrowIfCancellationRequested();

            foreach (var found in ScanDirectory(drive.RootDirectory.FullName, maxDepth, ct))
                yield return found;
        }
    }

    private static IEnumerable<StudioScanResult> ScanDirectory(string dir, int depthRemaining, CancellationToken ct)
    {
        if (depthRemaining <= 0)
            yield break;

        ct.ThrowIfCancellationRequested();

        var dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (IsNoiseDirectory(dirName))
            yield break;

        string exePath;
        try
        {
            exePath = Path.Combine(dir, KoroneConfig.StudioExecutableName);
        }
        catch (Exception)
        {
            yield break;
        }

        if (File.Exists(exePath))
        {
            var year = InferYearFromPath(dir);
            if (year is not null)
                yield return new StudioScanResult(year, dir);
        }

        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(dir);
        }
        catch (Exception)
        {
            yield break; // no permission, or the directory vanished mid-scan - just skip it
        }

        foreach (var sub in subdirs)
        {
            ct.ThrowIfCancellationRequested();

            IEnumerable<StudioScanResult> nested;
            try
            {
                nested = ScanDirectory(sub, depthRemaining - 1, ct).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var found in nested)
                yield return found;
        }
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

    private static bool IsNoiseDirectory(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var noise = new[]
        {
            "$Recycle.Bin", "System Volume Information", "Windows", "ProgramData",
            "node_modules", ".git", "proc", "sys", "dev",
        };
        return noise.Contains(name, StringComparer.OrdinalIgnoreCase) || name.StartsWith('.');
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
    /// downloading the full ZIP just to check for updates, which isn't a good trade for a
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

    public static void Launch(string installPath)
    {
        var exePath = Path.Combine(installPath, KoroneConfig.StudioExecutableName);
        ProcessLauncher.Launch(exePath);
    }
}
