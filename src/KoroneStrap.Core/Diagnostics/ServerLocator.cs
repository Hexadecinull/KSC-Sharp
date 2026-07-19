using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using KSCSharp.Core.Models;
using KSCSharp.Core.Platform;

namespace KSCSharp.Core.Diagnostics;

public record ServerLocation(string Ip, string? City, string? Region, string? Country, string? Isp);

public static class ServerLocator
{
    // Log directory ("%LocalAppData%\Roblox\logs\") is CONFIRMED real - Korone's own official
    // KoroneStrap Discord RPC client (github.com/.../KoroneStrap) watches that exact path. What's
    // still unverified is the line format within those logs: Roblox's client log contains a
    // line like:
    //   ... [FLog::Network] UDMUX Address = 128.116.15.100, StartTime = ...
    // when it joins a game server. This is publicly documented behavior (it's what every
    // "Roblox server region" browser extension / Discord bot parses), and Korone being
    // Roblox-compatible it very likely logs the same way - but that specific line hasn't been
    // confirmed against a real Korone log. If it doesn't match, this regex is the one place to
    // fix once a real sample log is available.
    private static readonly Regex ServerAddressPattern =
        new(@"UDMUX Address\s*=\s*([0-9]{1,3}(?:\.[0-9]{1,3}){3})", RegexOptions.Compiled);

    /// <summary>
    /// The confirmed-real logs directory for the current platform - Korone's own official
    /// Discord RPC client watches this exact path on Windows. Exposed for
    /// KoroneActivityWatcher, which needs a single directory to watch rather than the full
    /// fallback list FindLatestLogFile() checks.
    /// </summary>
    public static string PrimaryLogsDirectory => LogRoots().First();

    /// <summary>Finds the most recently modified client log file, across every install found.</summary>
    public static string? FindLatestLogFile()
    {
        string? latestPath = null;
        DateTime latestTime = DateTime.MinValue;

        foreach (var root in LogRoots())
        {
            if (!Directory.Exists(root))
                continue;

            IEnumerable<string> files;
            try { files = Directory.GetFiles(root, "*.log"); }
            catch (Exception) { continue; }

            foreach (var file in files)
            {
                var written = File.GetLastWriteTimeUtc(file);
                if (written > latestTime)
                {
                    latestTime = written;
                    latestPath = file;
                }
            }
        }

        return latestPath;
    }

    /// <summary>Scans a log file for the last (most recent) joined-server IP address.</summary>
    public static string? FindServerIpInLog(string logPath)
    {
        try
        {
            string? last = null;
            foreach (var line in File.ReadLines(logPath))
            {
                var match = ServerAddressPattern.Match(line);
                if (match.Success)
                    last = match.Groups[1].Value;
            }
            return last;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Geolocates an IP via a public API (ip-api.com's free tier - no key, no proxy, and no
    /// involvement from pekora.zip needed: this is a direct lookup against the server IP
    /// itself, same as pasting the IP into any "where is this server" website).
    /// </summary>
    public static async Task<ServerLocation?> LocateAsync(string ip, HttpClient? httpClient = null)
    {
        var http = httpClient ?? new HttpClient();
        try
        {
            using var response = await http.GetAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName,city,isp,query");
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var status) && status.GetString() != "success")
                return null;

            return new ServerLocation(
                Ip: ip,
                City: root.TryGetProperty("city", out var city) ? city.GetString() : null,
                Region: root.TryGetProperty("regionName", out var region) ? region.GetString() : null,
                Country: root.TryGetProperty("country", out var country) ? country.GetString() : null,
                Isp: root.TryGetProperty("isp", out var isp) ? isp.GetString() : null);
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

    /// <summary>Convenience: find the latest log, extract the server IP, and geolocate it in one call.</summary>
    public static async Task<ServerLocation?> QueryCurrentServerAsync()
    {
        var logFile = FindLatestLogFile();
        if (logFile is null)
            return null;

        var ip = FindServerIpInLog(logFile);
        if (ip is null)
            return null;

        return await LocateAsync(ip);
    }

    private static IEnumerable<string> LogRoots()
    {
        // "Roblox" is CONFIRMED (not guessed) - Korone's own official Discord RPC client
        // (KoroneStrap, K-major) watches %LocalAppData%\Roblox\logs\ directly. ProjectX/Pekora
        // are kept as fallback candidates in case a given install differs.
        var folders = new[] { "Roblox" }.Concat(KoroneConfig.InstallFolderNames);

        if (SystemInfo.IsWindows)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (var folder in folders)
                yield return Path.Combine(localAppData, folder, "logs");
            yield break;
        }

        var user = Environment.UserName;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var folder in folders)
            yield return Path.Combine(home, ".wine", "drive_c", "users", user, "AppData", "Local", folder, "logs");

        foreach (var folder in folders)
            yield return Path.Combine(home, ".local", "share", "wineprefixes", folder.ToLowerInvariant(), "drive_c", "users", user, "AppData", "Local", folder, "logs");
    }
}
