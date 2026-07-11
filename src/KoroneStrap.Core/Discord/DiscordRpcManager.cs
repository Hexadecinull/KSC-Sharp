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

    public bool Connect() => _client.TryConnect(KoroneConfig.DiscordClientId);

    public bool SetActivity(DiscordActivity activity) => _client.SetActivity(activity, _processId);

    public bool ClearActivity() => _client.ClearActivity(_processId);

    public void Disconnect() => _client.Disconnect();

    public void Dispose() => _client.Dispose();
}
