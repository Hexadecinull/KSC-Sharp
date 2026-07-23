using System.Text;
using System.Text.Json;

namespace KSCSharp.Core.Discord;

/// <summary>
/// Thin wrapper around DiscordIpcClient that the UI drives. One instance per app run;
/// Connect()/Disconnect() as the user toggles "Show game activity", SetActivity()/
/// ClearActivity() as clients launch and exit.
/// </summary>
public sealed class DiscordRpcManager : IDisposable
{
    private readonly DiscordIpcClient _client = new();
    private readonly int _processId = Environment.ProcessId;

    public bool IsConnected => _client.IsConnected;

    /// <summary>
    /// Fired when a friend clicks "Ask to Join" on this app's activity, with the placeId and
    /// client year to launch (decoded from the join secret this app itself generated - see
    /// EncodeJoinSecret). Delivered from a background thread; UI code must marshal back to the
    /// UI thread itself.
    /// </summary>
    public event Action<string, string>? JoinRequested;

    public DiscordRpcManager()
    {
        _client.ActivityJoinRequested += OnActivityJoinRequested;
    }

    public bool Connect()
    {
        if (!_client.TryConnect(KoroneConfig.DiscordClientId))
            return false;

        // Best-effort - if this fails, activity joining just won't fire the JoinRequested
        // event, same as if the user had it turned off.
        _client.Subscribe("ACTIVITY_JOIN");
        return true;
    }

    public bool SetActivity(DiscordActivity activity) => _client.SetActivity(activity, _processId);

    public bool ClearActivity() => _client.ClearActivity(_processId);

    public void Disconnect() => _client.Disconnect();

    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Encodes a placeId + client year into an opaque-looking join secret. This app is both the
    /// producer (SetActivity) and consumer (ACTIVITY_JOIN) of this value, so it just needs to be
    /// something we can reconstruct a launch from - no cryptographic property is needed here,
    /// it never leaves Discord's own systems as anything other than an opaque string to
    /// everyone except this app.
    /// </summary>
    public static string EncodeJoinSecret(string placeId, string year)
    {
        var json = JsonSerializer.Serialize(new { placeId, year });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Reverses EncodeJoinSecret. Returns null if the secret isn't one this app produced (e.g. garbage, or a future format change).</summary>
    public static (string PlaceId, string Year)? TryDecodeJoinSecret(string secret)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(secret));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("placeId", out var placeEl) && placeEl.ValueKind == JsonValueKind.String &&
                root.TryGetProperty("year", out var yearEl) && yearEl.ValueKind == JsonValueKind.String)
            {
                var placeId = placeEl.GetString();
                var year = yearEl.GetString();
                if (placeId is not null && year is not null)
                    return (placeId, year);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnActivityJoinRequested(string secret)
    {
        var decoded = TryDecodeJoinSecret(secret);
        if (decoded is not null)
            JoinRequested?.Invoke(decoded.Value.PlaceId, decoded.Value.Year);
    }
}
