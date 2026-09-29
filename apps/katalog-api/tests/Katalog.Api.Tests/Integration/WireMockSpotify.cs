using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Katalog.Api.Tests.Integration;

/// <summary>
/// Minimal Spotify Web API mock (WireMock) covering the endpoints Katalog calls:
/// POST /api/token (client credentials exchange) and the catalog GETs.
/// </summary>
public sealed class WireMockSpotify : IAsyncDisposable
{
    public WireMockServer Server { get; } = WireMockServer.Start();

    public string BaseUrl => Server.Urls[0];

    /// <summary>Clears all mappings, scenarios and logs so each test starts clean.</summary>
    public void Reset() => Server.Reset();

    public void StubArtistAlbums(string artistId, string artistName = "Artist One", params string[] albumIds)
    {
        var items = string.Join(",", albumIds.Select((id, i) => AlbumItemJson(artistId, id, $"Album {i + 1}", 2010, artistName)));
        var body = $$"""
            {"items": [{{items}}], "next": null, "total": {{albumIds.Length}}}
            """;
        Server.Given(Request.Create().WithPath($"/v1/artists/{artistId}/albums").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    /// <summary>Stubs the label-filtered album search used by release discovery.</summary>
    public void StubLabelSearch(string labelName, params string[] albumItems)
    {
        var searchJson = AlbumSearchJson(albumItems);
        Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
                .WithParam("q", $"label:\"{labelName}\"")
                .WithParam("type", "album")
                .WithParam("market", "SE")
                .WithParam("limit", "10"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(searchJson));
    }

    /// <summary>Stubs a specific page of the label-filtered album search.</summary>
    public void StubLabelSearchPage(string labelName, int offset, string? next, params string[] albumItems)
    {
        var searchJson = AlbumSearchJson(albumItems, next, offset);
        Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
                .WithParam("q", $"label:\"{labelName}\"")
                .WithParam("type", "album")
                .WithParam("market", "SE")
                .WithParam("limit", "10")
                .WithParam("offset", offset.ToString()))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(searchJson));
    }

    /// <summary>
    /// Stubs the full album object for GET /albums/{id} (one per search candidate).
    /// <paramref name="label"/> is the album's real Spotify label, which release discovery
    /// uses to verify the candidate exactly before upserting.
    /// </summary>
    public void StubAlbumGet(string albumId, string? label, string artistId = "artist1", string? name = null,
        int releaseYear = 2010, string artistName = "Artist One")
    {
        Server.Given(Request.Create().WithPath($"/v1/albums/{albumId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AlbumJson(albumId, label, artistId, name, releaseYear, artistName)));
    }

    /// <summary>
    /// Stubs the full album object with the real label carried in <c>copyrights</c> instead
    /// of the top-level <c>label</c> field - the shape Spotify currently returns for many
    /// albums (e.g. "2025 Globuli").
    /// </summary>
    public void StubAlbumGetByCopyright(string albumId, string label, string artistId = "artist1", string? name = null,
        int releaseYear = 2010, string artistName = "Artist One", string? copyrightSuffix = null)
    {
        Server.Given(Request.Create().WithPath($"/v1/albums/{albumId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AlbumJsonWithoutLabel(albumId, label, artistId, name, releaseYear, artistName, copyrightSuffix)));
    }

    public void StubTokenExchange(string accessToken = "test-access-token", int expiresIn = 3600)
    {
        Server.Given(Request.Create().WithPath("/api/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{expiresIn}}}"""));
    }

    public static string ArtistJson(string id, string name, int popularity = 50) =>
        $$"""
        {
          "id": "{{id}}",
          "name": "{{name}}",
          "images": [{"url": "https://i.scdn.co/image/{{id}}" }],
          "external_urls": {"spotify": "https://open.spotify.com/artist/{{id}}"},
          "genres": ["electronic"],
          "popularity": {{popularity}}
        }
        """;

    public static string AlbumsJson(string artistId, params string[] albumIds) =>
        $$"""
        {
          "items": [{{string.Join(",", albumIds.Select((id, i) => AlbumItemJson(artistId, id, $"Album {i + 1}", 2010)))}}],
          "next": null,
          "total": {{albumIds.Length}}
        }
        """;

    /// <summary>Spotify search response body (type=album): a page of simplified album items.</summary>
    public static string AlbumSearchJson(params string[] albumItems) =>
        AlbumSearchJson(albumItems, null, 0);

    public static string AlbumSearchJson(string[] albumItems, string? next, int offset)
    {
        var nextJson = next is null ? "null" : $"\"{next}\"";
        return $"{{\"albums\": {{\"items\": [{string.Join(",", albumItems)}], \"total\": {albumItems.Length}, \"next\": {nextJson}, \"offset\": {offset}}}}}";
    }

    public static string AlbumItemJson(string artistId, string id, string name, int releaseYear, string artistName = "Artist One", string? label = null)
    {
        var labelJson = label is null ? string.Empty : $",\n  \"label\": \"{label}\"";
        return $$"""
        {
          "id": "{{id}}",
          "name": "{{name}}",
          "album_type": "album",
          "release_date": "{{releaseYear:D4}}-01-15",
          "release_date_precision": "day",
          "images": [{"url": "https://i.scdn.co/image/{{id}}" }],
          "external_urls": {"spotify": "https://open.spotify.com/album/{{id}}"},
          "total_tracks": 10,
          "artists": [{"id": "{{artistId}}", "name": "{{artistName}}"}]{{labelJson}}
        }
        """;
    }

    /// <summary>Full album object (GET /albums/{id}); includes the real label field.</summary>
    public static string AlbumJson(string id, string? label, string artistId = "artist1", string? name = null,
        int releaseYear = 2010, string artistName = "Artist One") =>
        AlbumItemJson(artistId, id, name ?? $"Album {id}", releaseYear, artistName, label);

    /// <summary>Full album object where the real label only appears in the copyrights array.</summary>
    public static string AlbumJsonWithoutLabel(string id, string label, string artistId = "artist1", string? name = null,
        int releaseYear = 2010, string artistName = "Artist One", string? copyrightSuffix = null) =>
        $$"""
        {
          "id": "{{id}}",
          "name": "{{name ?? $"Album {id}"}}",
          "album_type": "album",
          "release_date": "{{releaseYear:D4}}-01-15",
          "release_date_precision": "day",
          "images": [{"url": "https://i.scdn.co/image/{{id}}" }],
          "external_urls": {"spotify": "https://open.spotify.com/album/{{id}}"},
          "total_tracks": 10,
          "artists": [{"id": "{{artistId}}", "name": "{{artistName}}"}],
          "copyrights": [
            {"text": "{{releaseYear}} {{label}}{{copyrightSuffix}}", "type": "P"},
            {"text": "{{releaseYear}} {{label}}", "type": "C"}
          ]
        }
        """;

    public async ValueTask DisposeAsync() => Server.Dispose();
}
