namespace KSCSharp.Core.Discord;

/// <summary>
/// Maps to the "activity" object in Discord's SET_ACTIVITY IPC command. Every field is
/// optional - only non-null ones get sent.
/// </summary>
public record DiscordActivity
{
    public string? State { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset? StartTimestamp { get; init; }

    public string? LargeImageKey { get; init; }
    public string? LargeImageText { get; init; }
    public string? SmallImageKey { get; init; }
    public string? SmallImageText { get; init; }

    /// <summary>Party id + current/max size, shown as "(2 of 4)" under the activity.</summary>
    public string? PartyId { get; init; }
    public int? PartySize { get; init; }
    public int? PartyMax { get; init; }

    /// <summary>Opaque secret a friend's client sends back if they click "Ask to Join".</summary>
    public string? JoinSecret { get; init; }

    /// <summary>
    /// Up to 2 (Label, Url) buttons shown on the activity card. Discord requires http(s) URLs
    /// here - a custom pekora-player:// URI isn't accepted.
    /// </summary>
    public IReadOnlyList<(string Label, string Url)>? Buttons { get; init; }
}
