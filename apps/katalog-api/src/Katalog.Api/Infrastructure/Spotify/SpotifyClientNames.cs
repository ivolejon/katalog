namespace Katalog.Api.Infrastructure.Spotify;

public static class SpotifyClientNames
{
    /// <summary>HttpClient used for the client-credentials token exchange against accounts.spotify.com.</summary>
    public const string Accounts = "spotify-accounts";

    /// <summary>Typed client used for catalog calls against api.spotify.com.</summary>
    public const string Catalog = "spotify-catalog";
}
