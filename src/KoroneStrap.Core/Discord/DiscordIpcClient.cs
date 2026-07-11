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
/// </summary>
public sealed class DiscordIpcClient : IDisposable
{
    private const int OpHandshake = 0;
    private const int OpFrame = 1;

    private Stream? _stream;

    public bool IsConnected { get; private set; }

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
            ReadFrame();

            IsConnected = true;
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
        try { _stream?.Dispose(); }
        catch (Exception) { /* best-effort */ }
        _stream = null;
    }

    public void Dispose() => Disconnect();

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

        _stream.Write(header, 0, header.Length);
        _stream.Write(payload, 0, payload.Length);
        _stream.Flush();
    }

    private void ReadFrame()
    {
        var header = ReadExact(8);
        var length = BitConverter.ToInt32(header, 4);
        if (length > 0)
            ReadExact(length);
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
