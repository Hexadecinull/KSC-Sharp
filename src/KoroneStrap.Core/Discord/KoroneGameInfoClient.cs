using System.Net.Http;
using System.Text.Json;

namespace KSCSharp.Core.Discord;

public record KoroneGameInfo(string? Name, string? IconUrl);

/// <summary>
/// Resolves a placeId/universeId (obtained safely - see KoroneActivityWatcher/GameJoinInfo,
/// never from a credential) into a game name and icon URL, for the dynamic Discord presence
/// image and the "Play Korone" button.
///
/// IMPORTANT: the exact endpoint below is a best-effort guess, not a confirmed one. What IS
/// confirmed (found directly in the Korone-Bootstrapper source's Discord OAuth callback URL)
/// is that "www.pekora.zip/api/..." is a real, used API prefix on the main site rather than a
/// separate subdomain the way real Roblox splits games.roblox.com/thumbnails.roblox.com out.
/// Everything past that prefix here is inferred from Roblox's own equivalent public endpoint
/// shapes, not verified against a real Korone response.
///
/// This fails silently and safely: any error (wrong path, unexpected response shape, timeout)
/// just returns null, and callers fall back to the static app icon. If someone can open browser
/// dev tools on a real pekora.zip game page and capture the actual request that loads the game
/// icon/name, that's the one piece of information needed to make this fully reliable instead of
/// best-effort.
/// </summary>
public static class KoroneGameInfoClient
{
    private const string ApiBase = "https://www.pekora.zip/api";

    public static async Task<KoroneGameInfo?> TryGetGameInfoAsync(string? placeId, string? universeId, HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(placeId) && string.IsNullOrEmpty(universeId))
            return null;

        var http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var idParam = !string.IsNullOrEmpty(placeId) ? $"placeId={placeId}" : $"universeId={universeId}";

            // Try a couple of plausible shapes - a places-info endpoint and a games-info
            // endpoint, mirroring Roblox's own split between apis.roblox.com/universes and
            // games.roblox.com/v1/games. Both are unconfirmed guesses; the first one that
            // returns something parseable wins, and if neither does, this returns null.
            var candidateUrls = new[]
            {
                $"{ApiBase}/places/info?{idParam}",
                $"{ApiBase}/games/info?{idParam}",
            };

            foreach (var url in candidateUrls)
            {
                var info = await TryFetchAsync(http, url);
                if (info is not null)
                    return info;
            }

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

    private static async Task<KoroneGameInfo?> TryFetchAsync(HttpClient http, string url)
    {
        try
        {
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            // Tolerant of a few plausible shapes (flat object, or a Roblox-style {"data":[...]})
            // since the real one isn't confirmed.
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataArr) &&
                dataArr.ValueKind == JsonValueKind.Array && dataArr.GetArrayLength() > 0)
            {
                root = dataArr[0];
            }

            string? name = TryGetString(root, "name") ?? TryGetString(root, "title");
            string? iconUrl = TryGetString(root, "iconUrl") ?? TryGetString(root, "icon") ?? TryGetString(root, "thumbnailUrl") ?? TryGetString(root, "imageUrl");

            return (name is null && iconUrl is null) ? null : new KoroneGameInfo(name, iconUrl);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
