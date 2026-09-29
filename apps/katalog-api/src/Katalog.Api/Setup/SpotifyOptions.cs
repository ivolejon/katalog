namespace Katalog.Api.Setup;

/// <summary>
/// Spotify credentials (app-level client credentials token, plus the OAuth redirect target used
/// by Spotify Connect sign-in). ClientId/ClientSecret must come from user secrets or environment
/// variables; they are never committed to appsettings.json and never reach the web client.
/// </summary>
public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    public string BaseUrl { get; init; } = "https://api.spotify.com";
    public string AccountsBaseUrl { get; init; } = "https://accounts.spotify.com";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// OAuth redirect URI registered in the Spotify app dashboard. Defaults to the dev api
    /// (stable port 5192, see Katalog.AppHost/Resources/Api/KatalogApi.cs); Spotify accepts
    /// http://localhost redirect URIs. Must be changed for any other deployment.
    /// </summary>
    public string RedirectUri { get; init; } = "http://localhost:5192/api/spotify/auth/callback";

    /// <summary>
    /// Frontend origin the OAuth callback sends the browser back to (dev web port 5173).
    /// Only the connect/disconnect outcome rides along as a query parameter.
    /// </summary>
    public string WebBaseUrl { get; init; } = "http://localhost:5173";

    /// <summary>ISO 3166-1 alpha-2 market used for catalog calls (research §2.1: market is effectively required).</summary>
    public string Market { get; init; } = "SE";

    /// <summary>Artists returned per search request. Spotify caps search limit at 10 (spec, verified 2026-09-22).</summary>
    public const int SearchLimitDefault = 10;
    public const int SearchLimitMax = 10;
    public const int AlbumsLimitMax = 10;

    /// <summary>
    /// Scopes requested for Spotify Connect control. No <c>streaming</c> scope: the Web API has
    /// no audio streaming, Katalog only controls a device the user already plays music on.
    /// </summary>
    public const string PlaybackStateScope = "user-read-playback-state user-modify-playback-state";

    /// <summary>Name of the HttpClient used for per-user Spotify Connect calls.</summary>
    public const string UserClientName = "spotify-user";
}
