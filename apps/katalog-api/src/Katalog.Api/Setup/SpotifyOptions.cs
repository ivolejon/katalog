namespace Katalog.Api.Setup;

/// <summary>
/// Spotify client credentials (app-level token, no user OAuth in MVP - arch report §3.3).
/// ClientId/ClientSecret must come from user secrets or environment variables; they are
/// never committed to appsettings.json.
/// </summary>
public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    public string BaseUrl { get; init; } = "https://api.spotify.com";
    public string AccountsBaseUrl { get; init; } = "https://accounts.spotify.com";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 market used for catalog calls (research §2.1: market is effectively required).</summary>
    public string Market { get; init; } = "SE";

    /// <summary>Artists returned per search request. Max 50 per Spotify.</summary>
    public const int SearchLimitDefault = 10;
    public const int SearchLimitMax = 50;
    public const int AlbumsLimitMax = 50;
}
