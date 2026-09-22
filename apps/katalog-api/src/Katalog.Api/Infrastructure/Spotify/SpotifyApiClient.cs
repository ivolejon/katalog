namespace Katalog.Api.Infrastructure.Spotify;

/// <summary>
/// Typed client for the Spotify catalog API (api.spotify.com). The Bearer token and the one
/// 401-refresh-retry dance are handled by <see cref="SpotifyTokenHandler"/>; transient failures
/// (429/Retry-After, 5xx) are handled by the custom resilience pipeline configured in Setup.
/// </summary>
public interface ISpotifyApiClient
{
    Task<SpotifyArtist?> GetArtistAsync(string spotifyArtistId, CancellationToken cancellationToken);

    Task<SpotifySearchArtistsResponse> SearchArtistsAsync(string query, int limit, string market,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the artist discography (albums + singles, as configured by include_groups).
    /// Pages through the full result set; per-artist quota is respected by the caller's poll rhythm.
    /// </summary>
    Task<IReadOnlyList<SpotifyAlbumItem>> GetArtistAlbumsAsync(string spotifyArtistId, int limit, string market,
        CancellationToken cancellationToken);
}

public sealed class SpotifyApiClient(
    HttpClient httpClient,
    ILogger<SpotifyApiClient> logger) : ISpotifyApiClient
{
    public async Task<SpotifyArtist?> GetArtistAsync(string spotifyArtistId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"v1/artists/{spotifyArtistId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SpotifyArtist>(cancellationToken);
    }

    public async Task<SpotifySearchArtistsResponse> SearchArtistsAsync(string query, int limit, string market,
        CancellationToken cancellationToken)
    {
        var queryString = Uri.EscapeDataString(query);
        var response = await httpClient.GetAsync(
            $"v1/search?q={queryString}&type=artist&market={market}&limit={limit}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SpotifySearchArtistsResponse>(cancellationToken)
               ?? throw new SpotifyApiException("Spotify search returned an empty body.");
    }

    public async Task<IReadOnlyList<SpotifyAlbumItem>> GetArtistAlbumsAsync(string spotifyArtistId, int limit, string market,
        CancellationToken cancellationToken)
    {
        // Spotify caps get-an-artists-albums limit at 10 (spec, verified 2026-09-22); higher returns HTTP 400 "Invalid limit".
        // Pagination runs on the next-cursor, so clamping the initial page size is safe and never loses data.
        limit = Math.Min(limit, 10);

        var items = new List<SpotifyAlbumItem>();
        Uri? next = new($"v1/artists/{spotifyArtistId}/albums?include_groups=album,single&market={market}&limit={limit}",
            UriKind.Relative);

        while (next is not null)
        {
            var response = await httpClient.GetAsync(next, cancellationToken);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<SpotifyArtistAlbumsResponse>(cancellationToken)
                       ?? throw new SpotifyApiException("Spotify artist albums returned an empty body.");

            items.AddRange(page.Items);
            logger.LogDebug("Fetched {Count} albums for artist {ArtistId} (next: {HasNext}).", page.Items.Count, spotifyArtistId, page.Next is not null);
            next = page.Next is null ? null : ValidateNextPage(page.Next);
        }

        return items;
    }

    private Uri ValidateNextPage(string next)
    {
        if (string.IsNullOrWhiteSpace(next) || httpClient.BaseAddress is null
            || !Uri.TryCreate(httpClient.BaseAddress, next, out var resolved)
            || !IsSameOrigin(httpClient.BaseAddress, resolved)
            || !string.IsNullOrEmpty(resolved.UserInfo))
        {
            throw new SpotifyApiException("Spotify returned an invalid pagination URL.");
        }

        return resolved;
    }

    private static bool IsSameOrigin(Uri expected, Uri actual) =>
        string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase)
        && expected.Port == actual.Port;
}
