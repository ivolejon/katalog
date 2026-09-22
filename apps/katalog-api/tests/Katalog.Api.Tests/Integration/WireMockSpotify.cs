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

    public static string AlbumItemJson(string artistId, string id, string name, int releaseYear) =>
        $$"""
        {
          "id": "{{id}}",
          "name": "{{name}}",
          "album_type": "album",
          "release_date": "{{releaseYear:D4}}-01-15",
          "release_date_precision": "day",
          "images": [{"url": "https://i.scdn.co/image/{{id}}" }],
          "external_urls": {"spotify": "https://open.spotify.com/album/{{id}}"},
          "total_tracks": 10,
          "artists": [{"id": "{{artistId}}", "name": "Artist One"}]
        }
        """;

    public async ValueTask DisposeAsync() => Server.Dispose();
}
