using System.Text.Json.Serialization;

namespace Katalog.Api.Infrastructure.Spotify;

// Response shapes for the small subset of the Spotify Web API used by Katalog.
// Field names follow the published OpenAPI schema (research §1); the deprecated `label`
// field appears only on the full album object (GET /albums/{id}) which M1 does not call
// (arch report §3.4 keeps the raw label column for a later M3 enrichment).

public sealed record SpotifyTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record SpotifySearchArtistsResponse(
    [property: JsonPropertyName("artists")] SpotifyArtistPage Artists);

public sealed record SpotifySearchAlbumsResponse(
    [property: JsonPropertyName("albums")] SpotifyAlbumPage Albums);

public sealed record SpotifyAlbumPage(
    [property: JsonPropertyName("items")] IReadOnlyList<SpotifyAlbumItem> Items,
    [property: JsonPropertyName("total")] int Total);

public sealed record SpotifyArtistPage(
    [property: JsonPropertyName("items")] IReadOnlyList<SpotifyArtist> Items,
    [property: JsonPropertyName("total")] int Total);

public sealed record SpotifyArtist(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("images")] IReadOnlyList<SpotifyImage>? Images,
    [property: JsonPropertyName("external_urls")] SpotifyExternalUrls? ExternalUrls,
    [property: JsonPropertyName("genres")] IReadOnlyList<string>? Genres,
    [property: JsonPropertyName("popularity")] int? Popularity)
{
    public string? ImageUrl => Images is { Count: > 0 } ? Images[0].Url : null;
    public string? ExternalUrl => ExternalUrls?.Spotify;
}

public sealed record SpotifyArtistAlbumsResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<SpotifyAlbumItem> Items,
    [property: JsonPropertyName("next")] string? Next,
    [property: JsonPropertyName("total")] int Total);

/// <summary>Simplified album object returned by the artist discography endpoint (no label field).</summary>
public sealed record SpotifyAlbumItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("album_type")] string AlbumType,
    [property: JsonPropertyName("release_date")] string? ReleaseDate,
    [property: JsonPropertyName("release_date_precision")] string? ReleaseDatePrecision,
    [property: JsonPropertyName("images")] IReadOnlyList<SpotifyImage>? Images,
    [property: JsonPropertyName("external_urls")] SpotifyExternalUrls? ExternalUrls,
    [property: JsonPropertyName("total_tracks")] int TotalTracks,
    [property: JsonPropertyName("artists")] IReadOnlyList<SpotifyAlbumArtist>? Artists)
{
    public string? ImageUrl => Images is { Count: > 0 } ? Images[0].Url : null;
    public string? ExternalUrl => ExternalUrls?.Spotify;
}

public sealed record SpotifyAlbumArtist(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);

public sealed record SpotifyImage(
    [property: JsonPropertyName("url")] string Url);

public sealed record SpotifyExternalUrls(
    [property: JsonPropertyName("spotify")] string? Spotify);
