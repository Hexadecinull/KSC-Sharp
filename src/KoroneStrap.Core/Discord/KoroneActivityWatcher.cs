using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KSCSharp.Core.Diagnostics;

namespace KSCSharp.Core.Discord;

/// <summary>
/// Watches for Korone's own "KORONESTRAPSDK" log marker and decodes presenceStateChange
/// events from it - the exact mechanism Korone's official Discord RPC client (also called
/// "KoroneStrap", K-major - a different project from koroneStrap the Python bootstrapper this
/// whole app is a rewrite of) uses to learn what to show as the Discord "state" text.
///
/// This is deliberately the safe half of what the official client does. Its full pipeline
/// also authenticates to pekora.zip's API using a login ticket (passed to it via
/// --AuthenticationTicket=... by Korone's own bootstrapper) to poll for which specific game
/// and icon to display. KSC-Sharp does not do that: capturing or using an authentication
/// ticket to make API calls as the user is treated the same as reading account credentials
/// elsewhere in this project - a line this project doesn't cross without it being an explicit,
/// separate decision. What's implemented here is the log-tailing half only: a public,
/// documented, non-credential channel that Korone's own client itself reads from.
/// </summary>
public sealed class KoroneActivityWatcher : IDisposable
{
    private static readonly Regex LineMarkerPattern =
        new(@"\[\s*KORONESTRAPSDK\s*\]\s*\(?([A-Za-z0-9+/=]+)\)?", RegexOptions.Compiled);

    private readonly Action<string> _onPresenceState;
    private FileSystemWatcher? _watcher;
    private FileStream? _fileStream;
    private StreamReader? _reader;
    private string? _currentLogPath;
    private volatile bool _requestReopen;
    private CancellationTokenSource? _cts;
    private Task? _tailTask;

    public KoroneActivityWatcher(Action<string> onPresenceState)
    {
        _onPresenceState = onPresenceState;
    }

    /// <summary>Starts tailing the logs directory in the background. No-op if the directory doesn't exist yet.</summary>
    public void Start()
    {
        var logsDir = ServerLocator.PrimaryLogsDirectory;
        if (!Directory.Exists(logsDir))
            return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            _watcher = new FileSystemWatcher(logsDir)
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _watcher.Created += (_, e) => { if (IsLogFile(e.FullPath)) _requestReopen = true; };
            _watcher.Renamed += (_, e) => { if (IsLogFile(e.FullPath)) _requestReopen = true; };
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception)
        {
            // best-effort - tailing still works via polling below even without the watcher
        }

        _tailTask = Task.Run(() => TailLoopAsync(logsDir, ct), ct);
    }

    private async Task TailLoopAsync(string logsDir, CancellationToken ct)
    {
        try { await Task.Delay(300, ct); }
        catch (OperationCanceledException) { return; }

        if (!OpenLatestLog(logsDir))
            return;

        while (!ct.IsCancellationRequested)
        {
            string? line = null;
            try
            {
                line = _reader is null ? null : await _reader.ReadLineAsync(ct);
            }
            catch (Exception)
            {
                // stream may have gone away mid-read - fall through to reopen logic below
            }

            if (line is null)
            {
                if (_requestReopen || (_currentLogPath is not null && !File.Exists(_currentLogPath)))
                {
                    _requestReopen = false;
                    OpenLatestLog(logsDir, reopen: true);
                }

                try { await Task.Delay(200, ct); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            TryHandleLine(line);
        }
    }

    private static bool IsLogFile(string path)
    {
        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".log", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    private bool OpenLatestLog(string logsDir, bool reopen = false)
    {
        try
        {
            string? latest;
            try
            {
                latest = new DirectoryInfo(logsDir)
                    .GetFiles()
                    .Where(f => IsLogFile(f.FullName))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .FirstOrDefault()?.FullName;
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrEmpty(latest))
                return false;

            if (!reopen && _currentLogPath == latest && _fileStream is not null && _reader is not null)
                return true;

            DisposeStreams();

            _currentLogPath = latest;
            _fileStream = new FileStream(_currentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _reader = new StreamReader(_fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            _fileStream.Seek(0, SeekOrigin.End);
            _reader.DiscardBufferedData();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void TryHandleLine(string line)
    {
        var match = LineMarkerPattern.Match(line);
        if (!match.Success)
            return;

        try
        {
            var bytes = Convert.FromBase64String(match.Groups[1].Value.Trim());
            var json = Encoding.UTF8.GetString(bytes);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("activityType", out var typeEl) ||
                !doc.RootElement.TryGetProperty("value", out var valueEl))
                return;

            if (!string.Equals(typeEl.GetString(), "presenceStateChange", StringComparison.OrdinalIgnoreCase))
                return;

            var value = valueEl.GetString() ?? "";
            if (value.Length > 200)
                value = value[..200];

            _onPresenceState(value);
        }
        catch (Exception)
        {
            // malformed or unrelated marker line - ignore
        }
    }

    private void DisposeStreams()
    {
        try { _reader?.Dispose(); } catch (Exception) { }
        try { _fileStream?.Dispose(); } catch (Exception) { }
        _reader = null;
        _fileStream = null;
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch (Exception) { }
        try { _watcher?.Dispose(); } catch (Exception) { }
        DisposeStreams();
        _cts?.Dispose();
    }
}
