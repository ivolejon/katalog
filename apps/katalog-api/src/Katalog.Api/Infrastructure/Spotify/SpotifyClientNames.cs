namespace Katalog.Api.Infrastructure.Spotify;

public static class SpotifyClientNames
{
    /// <summary>HttpClient used for the client-credentials token exchange against accounts.spotify.com.</summary>
    public const string Accounts = "spotify-accounts";

    /// <summary>Typed client used for catalog calls against api.spotify.com.</summary>
    public const string Catalog = "spotify-catalog";

    /// <summary>
    /// HttpClient for per-user Spotify Connect player calls (api.spotify.com). Carries the
    /// signed-in user's own token via <see cref="User.SpotifyUserTokenHandler"/>.
    /// </summary>
    public const string UserPlayback = "spotify-user";

    /// <summary>
    /// HttpClient for the sign-in profile lookup (GET /v1/me). No token handler: the callback
    /// holds the freshly exchanged token and sets it explicitly.
    /// </summary>
    public const string Profile = "spotify-user-profile";
}
