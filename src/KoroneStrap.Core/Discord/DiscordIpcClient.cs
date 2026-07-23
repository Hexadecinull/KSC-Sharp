using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace KSCSharp.Core.Discord;

/// <summary>
/// Talks the Discord "discord-rpc" IPC protocol directly: an 8-byte frame header
/// (int32 opcode, int32 payload length, little-endian) followed by UTF-8 JSON, over a
/// named pipe on Windows (\\.\pipe\discord-ipc-N) or a Unix domain socket elsewhere
/// (various discord-ipc-N locations depending on how Discord was installed).
///
/// This is publicly documented protocol (the same one every discord-rpc / DiscordRPC.NET /
/// presence.nvim-style library implements) - no official SDK or NuGet package is required to
/// speak it, which is deliberate here: it keeps this dependency-free and inspectable.
///
/// Runs a continuous background read loop once connected (not just a single blocking read
/// during the handshake), since Discord can push unsolicited DISPATCH frames at any time -
/// specifically ACTIVITY_JOIN, which fires when a friend clicks "Ask to Join" on the activity
/// this app published. Reading it is what makes activity joining actually work end to end
/// (previously the join button existed but nothing ever handled a click on it).
/// </summary>
public sealed class DiscordIpcClient : IDisposable
{
    private const int OpHandshake = 0;
    private const int OpFrame = 1;
    private const int OpClose = 2;

    private Stream? _stream;
    private readonly object _writeLock = new();
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;

    public bool IsConnected { get; private set; }

    /// <summary>Raised when Discord reports the user clicked "Ask to Join" on this app's activity, with the join secret that was set.</summary>
    public event Action<string>? ActivityJoinRequested;

    public bool TryConnect(string clientId)
    {
        if (IsConnected)
            return true;

        _stream = OpenPipe();
        if (_stream is null)
            return false;

        try
        {
            var handshake = JsonSerializer.Serialize(new { v = 1, client_id = clientId });
            WriteFrame(OpHandshake, handshake);

            // Discord replies with a DISPATCH/READY frame on a successful handshake. We don't
            // need to parse it in detail - getting any frame back without the pipe throwing
            // means the connection is live.
            ReadFrameRaw();

            IsConnected = true;

            _readLoopCts = new CancellationTokenSource();
            _readLoopTask = Task.Run(() => ReadLoop(_readLoopCts.Token));

            return true;
        }
        catch (Exception)
        {
            Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Subscribes to a Discord IPC event (e.g. "ACTIVITY_JOIN"). Must be called after a
    /// successful TryConnect. The event itself arrives later via ActivityJoinRequested,
    /// delivered on the background read loop thread (not the caller's thread).
    /// </summary>
    public bool Subscribe(string evt)
    {
        if (!IsConnected)
            return false;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                cmd = "SUBSCRIBE",
                nonce = Guid.NewGuid().ToString(),
                evt,
            });

            WriteFrame(OpFrame, payload);
            return true;
        }
        catch (Exception)
        {
            Disconnect();
            return false;
        }
    }

    public bool SetActivity(DiscordActivity activity, int processId)
    {
        if (!IsConnected)
            return false;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                cmd = "SET_ACTIVITY",
                nonce = Guid.NewGuid().ToString(),
                args = new
                {
                    pid = processId,
                    activity = BuildActivityPayload(activity),
                },
            });

            WriteFrame(OpFrame, payload);
            return true;
        }
        catch (Exception)
        {
            Disconnect();
            return false;
        }
    }

    public bool ClearActivity(int processId)
    {
        if (!IsConnected)
            return false;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                cmd = "SET_ACTIVITY",
                nonce = Guid.NewGuid().ToString(),
                args = new { pid = processId, activity = (object?)null },
            });

            WriteFrame(OpFrame, payload);
            return true;
        }
        catch (Exception)
        {
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        IsConnected = false;

        try { _readLoopCts?.Cancel(); } catch (Exception) { }
        try { _stream?.Dispose(); }
        catch (Exception) { /* best-effort */ }
        _stream = null;
        _readLoopCts?.Dispose();
        _readLoopCts = null;
    }

    public void Dispose() => Disconnect();

    private void ReadLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                string? json;
                try
                {
                    json = ReadFrameRaw();
                }
                catch (Exception)
                {
                    // Stream closed or errored - stop the loop quietly. The next SetActivity/
                    // Subscribe call will observe IsConnected is now false via Disconnect below.
                    break;
                }

                if (json is null)
                    continue;

                TryHandleIncomingFrame(json);
            }
        }
        catch (Exception)
        {
            // background loop must never throw out to the thread pool
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                Disconnect();
        }
    }

    private void TryHandleIncomingFrame(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("evt", out var evtEl) || evtEl.ValueKind != JsonValueKind.String)
                return;

            if (!string.Equals(evtEl.GetString(), "ACTIVITY_JOIN", StringComparison.OrdinalIgnoreCase))
                return;

            if (root.TryGetProperty("data", out var dataEl) &&
                dataEl.TryGetProperty("secret", out var secretEl) &&
                secretEl.ValueKind == JsonValueKind.String)
            {
                var secret = secretEl.GetString();
                if (!string.IsNullOrEmpty(secret))
                    ActivityJoinRequested?.Invoke(secret);
            }
        }
        catch (Exception)
        {
            // malformed frame - ignore, keep the loop alive
        }
    }

    private static object BuildActivityPayload(DiscordActivity activity)
    {
        var dict = new Dictionary<string, object?>();

        if (activity.State is not null) dict["state"] = activity.State;
        if (activity.Details is not null) dict["details"] = activity.Details;

        if (activity.StartTimestamp is not null)
        {
            dict["timestamps"] = new Dictionary<string, object?>
            {
                ["start"] = activity.StartTimestamp.Value.ToUnixTimeSeconds(),
            };
        }

        if (activity.LargeImageKey is not null || activity.SmallImageKey is not null)
        {
            var assets = new Dictionary<string, object?>();
            if (activity.LargeImageKey is not null) assets["large_image"] = activity.LargeImageKey;
            if (activity.LargeImageText is not null) assets["large_text"] = activity.LargeImageText;
            if (activity.SmallImageKey is not null) assets["small_image"] = activity.SmallImageKey;
            if (activity.SmallImageText is not null) assets["small_text"] = activity.SmallImageText;
            dict["assets"] = assets;
        }

        if (activity.PartyId is not null)
        {
            var party = new Dictionary<string, object?> { ["id"] = activity.PartyId };
            if (activity.PartySize is not null && activity.PartyMax is not null)
                party["size"] = new[] { activity.PartySize.Value, activity.PartyMax.Value };
            dict["party"] = party;
        }

        if (activity.JoinSecret is not null)
            dict["secrets"] = new Dictionary<string, object?> { ["join"] = activity.JoinSecret };

        if (activity.Buttons is { Count: > 0 })
        {
            // Discord IPC only accepts up to 2 buttons; silently trim rather than throw, since
            // a caller passing 3 shouldn't crash the whole activity update over it.
            dict["buttons"] = activity.Buttons
                .Take(2)
                .Select(b => new Dictionary<string, object?> { ["label"] = b.Label, ["url"] = b.Url })
                .ToArray();
        }

        return dict;
    }

    private static Stream? OpenPipe()
    {
        if (OperatingSystem.IsWindows())
        {
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                    pipe.Connect(200);
                    return pipe;
                }
                catch (Exception)
                {
                    // try the next slot - Discord picks whichever is free, so we don't know
                    // which index it's actually listening on
                }
            }
            return null;
        }

        foreach (var dir in CandidateSocketDirectories())
        {
            for (var i = 0; i < 10; i++)
            {
                var path = Path.Combine(dir, $"discord-ipc-{i}");
                if (!File.Exists(path))
                    continue;

                try
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    socket.Connect(new UnixDomainSocketEndPoint(path));
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception)
                {
                    // socket file existed but nothing accepted the connection - keep looking
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateSocketDirectories()
    {
        var seen = new HashSet<string>();

        void Add(string? dir)
        {
            if (!string.IsNullOrEmpty(dir))
                seen.Add(dir);
        }

        var xdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        Add(xdgRuntime);
        if (!string.IsNullOrEmpty(xdgRuntime))
        {
            Add(Path.Combine(xdgRuntime, "app", "com.discordapp.Discord")); // Flatpak
            Add(Path.Combine(xdgRuntime, "snap.discord"));                 // Snap
        }

        Add(Environment.GetEnvironmentVariable("TMPDIR"));
        Add("/tmp");

        return seen;
    }

    private void WriteFrame(int opcode, string json)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected.");

        var payload = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(payload.Length).CopyTo(header, 4);

        // Guards against the foreground thread (SetActivity/Subscribe) and the background
        // read loop's own writes (there currently are none, but this stays cheap insurance)
        // interleaving mid-frame.
        lock (_writeLock)
        {
            if (_stream is null)
                throw new InvalidOperationException("Not connected.");

            _stream.Write(header, 0, header.Length);
            _stream.Write(payload, 0, payload.Length);
            _stream.Flush();
        }
    }

    /// <summary>Reads one frame and returns its JSON payload (or null for an empty/close frame).</summary>
    private string? ReadFrameRaw()
    {
        var header = ReadExact(8);
        var opcode = BitConverter.ToInt32(header, 0);
        var length = BitConverter.ToInt32(header, 4);

        if (length <= 0)
            return null;

        var payload = ReadExact(length);

        if (opcode == OpClose)
            throw new IOException("Discord sent a close frame.");

        return Encoding.UTF8.GetString(payload);
    }

    private byte[] ReadExact(int count)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected.");

        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = _stream.Read(buffer, read, count - read);
            if (n == 0)
                throw new IOException("Discord closed the connection.");
            read += n;
        }
        return buffer;
    }
}
