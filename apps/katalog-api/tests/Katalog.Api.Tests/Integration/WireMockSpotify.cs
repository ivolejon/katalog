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

    /* ---- Spotify Connect (signed-in user endpoints) ---- */

    /// <summary>
    /// Stubs the authorization-code exchange. <paramref name="expectVerifier"/> makes the stub
    /// match only when the request carries the PKCE verifier, which is what proves the callback
    /// belongs to the sign-in this client started.
    /// </summary>
    public void StubUserCodeExchange(string accessToken = "user-access-token", string refreshToken = "user-refresh-token",
        int expiresIn = 3600, string? expectVerifier = null)
    {
        // One body matcher: the form must carry the authorization-code grant, the code, and
        // (when the caller knows it) the PKCE verifier that proves this is our sign-in.
        var required = new List<string> { "grant_type=authorization_code", "code=the-code" };
        if (expectVerifier is not null)
        {
            required.Add($"code_verifier={expectVerifier}");
        }

        Server.Given(Request.Create().WithPath("/api/token").UsingPost()
                .WithBody(b => required.All(fragment => b?.Contains(fragment, StringComparison.Ordinal) == true)))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($"{{\"access_token\":\"{accessToken}\",\"token_type\":\"Bearer\",\"expires_in\":{expiresIn},\"refresh_token\":\"{refreshToken}\",\"scope\":\"user-read-playback-state user-modify-playback-state\"}}"));
    }

    /// <summary>Stubs the refresh-token grant used to renew a user's access token.</summary>
    public void StubUserRefreshGrant(string accessToken = "renewed-access-token", string refreshToken = "renewed-refresh-token",
        int expiresIn = 3600)
        => Server.Given(Request.Create().WithPath("/api/token").UsingPost()
                .WithBody(b => b?.Contains("grant_type=refresh_token", StringComparison.Ordinal) == true))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($"{{\"access_token\":\"{accessToken}\",\"token_type\":\"Bearer\",\"expires_in\":{expiresIn},\"refresh_token\":\"{refreshToken}\",\"scope\":\"user-read-playback-state user-modify-playback-state\"}}"));

    /// <summary>Stubs GET /v1/me, the profile lookup the callback makes with the fresh token.</summary>
    public void StubProfile(string userId = "captain", string displayName = "Captain", string product = "premium")
        => Server.Given(Request.Create().WithPath("/v1/me").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"id":"{{userId}}","display_name":"{{displayName}}","product":"{{product}}"}"""));

    /// <summary>Stubs GET /v1/me/player/devices with the user's devices.</summary>
    public void StubDevices(string accessToken = "user-access-token", params (string Id, string Name, string Type, bool IsActive)[] devices)
    {
        var items = string.Join(",", devices.Select(d =>
            $$"""{"id":"{{d.Id}}","is_active":{{d.IsActive.ToString().ToLowerInvariant()}},"is_private_session":false,"is_restricted":false,"name":"{{d.Name}}","type":"{{d.Type}}","volume_percent":50,"supports_volume":true}"""));
        Server.Given(Request.Create().WithPath("/v1/me/player/devices").UsingGet()
                .WithHeader("Authorization", $"Bearer {accessToken}"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"devices":[{{items}}]}"""));
    }

    /// <summary>Makes any devices call with this token answer 401, to drive the refresh + retry.</summary>
    public void StubUnauthorizedDevices(string exceptAccessToken)
    {
        Server.Given(Request.Create().WithPath("/v1/me/player/devices").UsingGet()
                .WithHeader("Authorization", $"Bearer {exceptAccessToken}"))
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":{"status":401,"message":"The access token expired"}}"""));
    }

    /// <summary>Stubs a successful PUT /v1/me/player/play (Spotify answers 204).</summary>
    public void StubPlay(string accessToken = "user-access-token")
        => Server.Given(Request.Create().WithPath("/v1/me/player/play").UsingPut()
                .WithHeader("Authorization", $"Bearer {accessToken}"))
            .RespondWith(Response.Create().WithStatusCode(204));

    /// <summary>
    /// Makes a play call made with <paramref name="rejectedAccessToken"/> answer 401, which is
    /// how the "Spotify rejected our token, renew and retry once" path is driven. Register it
    /// together with <see cref="StubPlay"/> for the renewed token; the retry then matches that
    /// one instead.
    /// </summary>
    public void StubUnauthorizedPlay(string rejectedAccessToken)
    {
        Server.Given(Request.Create().WithPath("/v1/me/player/play").UsingPut()
                .WithHeader("Authorization", $"Bearer {rejectedAccessToken}"))
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"error\":{\"status\":401,\"message\":\"The access token expired\"}}"));
    }

    /// <summary>Stubs a player call that fails with Spotify's error shape.</summary>
    public void StubPlayerFailure(int statusCode, string reason, string message, string path = "/v1/me/player/play")
        => Server.Given(Request.Create().WithPath(path).UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody($"{{\"error\":{{\"status\":{statusCode},\"message\":\"{message}\",\"reason\":\"{reason}\"}}}}"));

    /// <summary>Stubs the current playback state (GET /v1/me/player).</summary>
    public void StubPlaybackState(string contextUri, bool isPlaying = true, string deviceId = "device1")
        => Server.Given(Request.Create().WithPath("/v1/me/player").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"is_playing\":" + Json(isPlaying)
                    + ",\"device\":{\"id\":\"" + deviceId + "\",\"is_active\":true,\"name\":\"Kitchen\",\"type\":\"speaker\"}"
                    + ",\"context\":{\"uri\":\"" + contextUri + "\",\"type\":\"album\"}}"));

    /// <summary>Stubs "nothing is playing anywhere", which Spotify answers with 204.</summary>
    public void StubNoPlayback()
        => Server.Given(Request.Create().WithPath("/v1/me/player").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(204));

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

    /// <summary>
    /// Stubs the full album object with an optional top-level label and a custom set of
    /// copyright lines - used to verify label-aware extraction picks the copyright line
    /// that matches the followed label rather than blindly returning the first parsed line.
    /// </summary>
    public void StubAlbumGetWithCopyrights(string albumId, string? label, string[] copyrights, string artistId = "artist1",
        string? name = null, int releaseYear = 2010, string artistName = "Artist One")
    {
        Server.Given(Request.Create().WithPath($"/v1/albums/{albumId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AlbumJsonWithCopyrights(albumId, label, copyrights, artistId, name, releaseYear, artistName)));
    }

    public static string AlbumJsonWithCopyrights(string id, string? label, string[] copyrights, string artistId = "artist1",
        string? name = null, int releaseYear = 2010, string artistName = "Artist One")
    {
        var labelJson = label is null ? string.Empty : $",\n  \"label\": \"{label}\"";
        var copyrightItems = string.Join(",", copyrights.Select(c => $"{{\"text\": \"{c}\", \"type\": \"C\"}}"));
        var albumName = name ?? $"Album {id}";
        return $$"""
        {
          "id": "{{id}}",
          "name": "{{albumName}}",
          "album_type": "album",
          "release_date": "{{releaseYear:D4}}-01-15",
          "release_date_precision": "day",
          "images": [{"url": "https://i.scdn.co/image/{{id}}" }],
          "external_urls": {"spotify": "https://open.spotify.com/album/{{id}}"},
          "total_tracks": 10,
          "artists": [{"id": "{{artistId}}", "name": "{{artistName}}"}],
          "copyrights": [{{copyrightItems}}]{{labelJson}}
        }
        """;
    }
    /// <summary>User profile shape (GET /v1/me).</summary>
    public static string ProfileJson(string id, string displayName, string product = "premium")
        => $"{{\"id\":\"{id}\",\"display_name\":\"{displayName}\",\"product\":\"{product}\"}}";

    /// <summary>Spotify Connect device list shape (GET /v1/me/player/devices).</summary>
    public static string DevicesJson(params (string Id, string Name, string Type, bool IsActive)[] devices)
    {
        var items = string.Join(",", devices.Select(d =>
            "{\"id\":\"" + d.Id + "\",\"is_active\":" + Json(d.IsActive)
            + ",\"is_private_session\":false,\"is_restricted\":false,\"name\":\"" + d.Name
            + "\",\"type\":\"" + d.Type + "\",\"volume_percent\":50,\"supports_volume\":true}"));
        return $"{{\"devices\":[{items}]}}";
    }

    private static string Json(bool value) => value ? "true" : "false";

    public async ValueTask DisposeAsync() => Server.Dispose();
}
