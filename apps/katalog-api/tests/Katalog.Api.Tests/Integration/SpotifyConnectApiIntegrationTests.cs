using System.Net;
using System.Net.Http.Json;
using System.Web;
using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Katalog.Api.Tests.Integration;

/// <summary>
/// Spotify Connect end to end against WireMock: the sign-in redirect, the callback that
/// exchanges the code and stores the tokens, the device list, and play/pause on a device -
/// including the two failures the UI must be able to explain (no active device, no Premium)
/// and the 401 refresh-and-retry path. No real Spotify account is involved.
/// </summary>
public sealed class SpotifyConnectApiIntegrationTests(PostgresFixture postgres, WireMockSpotify spotify)
    : IClassFixture<PostgresFixture>, IClassFixture<WireMockSpotify>
{
    private const string AlbumId = "albumone";

    [Fact]
    public async Task Login_RedirectsToSpotifyWithPlaybackScopesStateAndPkce()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = NoRedirectClient(factory);

        var response = await client.GetAsync("/api/spotify/auth/login");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        // Read the raw header, not the parsed Uri: Uri.ToString() unescapes %20 and would hide
        // a literal space in the header Spotify actually receives.
        var location = Assert.Single(response.Headers.GetValues("Location"));
        Assert.StartsWith($"{spotify.BaseUrl}/authorize?", location);

        // A scope is space-separated: it must arrive percent-encoded. A literal space in the
        // Location header would make Spotify read only the first scope.
        Assert.DoesNotContain(' ', location);

        var query = ParseQuery(location);
        Assert.Equal("test-client-id", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("http://localhost:5192/api/spotify/auth/callback", query["redirect_uri"]);
        // No streaming scope: the Web API cannot stream audio, Katalog only controls a device.
        Assert.Equal("user-read-playback-state user-modify-playback-state", query["scope"]);
        Assert.False(query["scope"].Contains("streaming", StringComparison.Ordinal));
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.NotEmpty(query["state"]);
        Assert.NotEmpty(query["code_challenge"]);

        // The client secret must never reach the browser.
        Assert.DoesNotContain("test-client-secret", location, StringComparison.Ordinal);

        // The PKCE verifier is kept in an HttpOnly cookie, not in the URL.
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("katalog_oauth=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Callback_ExchangesTheCodeWithTheVerifier_StoresTokensServerSide_AndReturnsTheApp()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = NoRedirectClient(factory);

        var handshake = await BeginSignInAsync(client);
        Assert.NotNull(handshake);
        var (state, verifier) = handshake!.Value;

        // The exchange must prove it is the flow we started (the code_verifier from the cookie).
        spotify.StubUserCodeExchange("user-access-token", "user-refresh-token", expectVerifier: verifier);
        spotify.StubProfile("captain", "Captain", "premium");

        var callback = await client.GetAsync($"/api/spotify/auth/callback?code=the-code&state={HttpUtility.UrlEncode(state)}");

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal("http://localhost:5173/?spotify=connected", callback.Headers.Location!.ToString());

        // Tokens are stored server-side, one row for the account, never handed to the browser.
        var session = await client.GetFromJsonAsync<SpotifySessionResponse>("/api/spotify/me");
        Assert.NotNull(session);
        Assert.True(session!.IsConnected);
        Assert.Equal("captain", session.SpotifyUserId);
        Assert.Equal("Captain", session.DisplayName);
        Assert.Equal("premium", session.Product);

        var raw = await client.GetStringAsync("/api/spotify/me");
        Assert.DoesNotContain("user-access-token", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("user-refresh-token", raw, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
        var row = await context.SpotifyUserSessions.SingleAsync();
        Assert.Equal("user-access-token", row.AccessToken);
        Assert.Equal("user-refresh-token", row.RefreshToken);
        Assert.True(row.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Callback_WithAMismatchedState_StoresNothing()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = NoRedirectClient(factory);

        await BeginSignInAsync(client);
        spotify.StubUserCodeExchange();
        spotify.StubProfile();

        var callback = await client.GetAsync("/api/spotify/auth/callback?code=the-code&state=forged");

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal("http://localhost:5173/?spotify=failed&reason=state_mismatch", callback.Headers.Location!.ToString());

        var session = await client.GetFromJsonAsync<SpotifySessionResponse>("/api/spotify/me");
        Assert.False(session!.IsConnected);
        Assert.DoesNotContain(spotify.Server.LogEntries, l => l.RequestMessage.Path == "/api/token");
    }

    [Fact]
    public async Task Callback_WithoutAnySignInAttempt_StoresNothing()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = NoRedirectClient(factory);

        var callback = await client.GetAsync("/api/spotify/auth/callback?code=the-code&state=whatever");

        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal("http://localhost:5173/?spotify=failed&reason=no_sign_in_attempt", callback.Headers.Location!.ToString());
        Assert.False((await client.GetFromJsonAsync<SpotifySessionResponse>("/api/spotify/me"))!.IsConnected);
    }

    [Fact]
    public async Task Devices_AreListedWithTheActiveDeviceFlagged()
    {
        spotify.Reset();
        spotify.StubDevices(devices:
        [
            ("device1", "Kitchen speaker", "speaker", false),
            ("device2", "Living Room TV", "tv", true),
        ]);

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.GetFromJsonAsync<SpotifyDevicesResponse>("/api/spotify/devices");

        Assert.NotNull(response);
        Assert.Equal("device2", response!.ActiveDeviceId);
        Assert.Equal(2, response.Devices.Count);
        Assert.Equal("Kitchen speaker", response.Devices[0].Name);
        Assert.Equal("speaker", response.Devices[0].Type);
        Assert.True(response.Devices[1].IsActive);
    }

    [Fact]
    public async Task Play_SendsTheAlbumContextAndTheChosenDevice()
    {
        spotify.Reset();
        spotify.StubPlay();

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/spotify/playback/play",
            new { spotifyAlbumId = AlbumId, deviceId = "device1" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var play = Assert.Single(spotify.Server.LogEntries, l => l.RequestMessage.Path == "/v1/me/player/play");
        Assert.Equal("PUT", play.RequestMessage.Method);
        // device_id is a query parameter on the player endpoints (published spec).
        Assert.Equal("device1", play.RequestMessage.Query!["device_id"].First());
        // The album is the playback context; no extra album lookup is made.
        Assert.Contains($"\"context_uri\":\"spotify:album:{AlbumId}\"", play.RequestMessage.Body!, StringComparison.Ordinal);
        Assert.DoesNotContain(spotify.Server.LogEntries, l => l.RequestMessage.Path.StartsWith("/v1/albums", StringComparison.Ordinal));

        // The user's own token is used, never the app's client credentials token.
        Assert.Equal("Bearer user-access-token", play.RequestMessage.Headers!["Authorization"].First());
    }

    [Fact]
    public async Task Play_WithoutADevice_UsesTheUsersActiveDevice()
    {
        spotify.Reset();
        spotify.StubPlay();

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/spotify/playback/play", new { spotifyAlbumId = AlbumId, deviceId = (string?)null });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var play = Assert.Single(spotify.Server.LogEntries, l => l.RequestMessage.Path == "/v1/me/player/play");
        Assert.DoesNotContain(play.RequestMessage.Query!.Keys, key => key == "device_id");
    }

    [Fact]
    public async Task Play_WithoutAnActiveDevice_AnswersNotFoundWithGuidance()
    {
        spotify.Reset();
        spotify.StubPlayerFailure(404, "NO_ACTIVE_DEVICE", "No active device found");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/spotify/playback/play", new { spotifyAlbumId = AlbumId, deviceId = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("No active Spotify device.", problem.Title);
        Assert.Contains("Spotify app", problem.Detail!, StringComparison.Ordinal);
        // Extensions deserialize as JsonElement, so compare its text.
        Assert.Equal("NO_ACTIVE_DEVICE", problem.Extensions!["reason"]!.ToString());
    }

    [Fact]
    public async Task Play_WithoutPremium_AnswersForbiddenWithGuidance()
    {
        spotify.Reset();
        spotify.StubPlayerFailure(403, "PREMIUM_REQUIRED", "Player command failed: Premium required");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/spotify/playback/play", new { spotifyAlbumId = AlbumId, deviceId = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Spotify Premium is required.", problem.Title);
        Assert.Contains("Premium", problem.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Play_WithARestrictedDevice_AnswersForbidden()
    {
        spotify.Reset();
        spotify.StubPlayerFailure(403, "RESTRICTED_DEVICE", "Device not controllable");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/spotify/playback/play", new { spotifyAlbumId = AlbumId, deviceId = "device1" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("That Spotify device cannot be controlled.", problem.Title);
    }

    [Fact]
    public async Task Pause_SendsTheDeviceToSpotify()
    {
        spotify.Reset();
        spotify.Server.Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/v1/me/player/pause").UsingPut())
            .RespondWith(WireMock.ResponseBuilders.Response.Create().WithStatusCode(204));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/spotify/playback/pause", new { deviceId = "device1" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var pause = Assert.Single(spotify.Server.LogEntries, l => l.RequestMessage.Path == "/v1/me/player/pause");
        Assert.Equal("device1", pause.RequestMessage.Query!["device_id"].First());
    }

    [Fact]
    public async Task PlaybackState_ReportsThePlayingContext()
    {
        spotify.Reset();
        spotify.StubPlaybackState($"spotify:album:{AlbumId}", isPlaying: true, deviceId: "device1");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var state = await client.GetFromJsonAsync<SpotifyPlaybackStateResponse>("/api/spotify/playback");

        Assert.NotNull(state);
        Assert.True(state!.IsPlaying);
        Assert.Equal($"spotify:album:{AlbumId}", state.ContextUri);
        Assert.Equal("album", state.ContextType);
        Assert.Equal("device1", state.DeviceId);
    }

    [Fact]
    public async Task PlaybackState_WhenNothingPlays_IsReportedAsStopped()
    {
        spotify.Reset();
        spotify.StubNoPlayback();

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var state = await client.GetFromJsonAsync<SpotifyPlaybackStateResponse>("/api/spotify/playback");

        Assert.NotNull(state);
        Assert.False(state!.IsPlaying);
        Assert.Null(state.ContextUri);
    }

    [Fact]
    public async Task Play_WhenSpotifyAnswers401_RenewsTheTokenAndRetriesOnce()
    {
        spotify.Reset();
        // Registered first so the 401 mapping wins until the token is renewed; the specific
        // mapping then answers the retry.
        spotify.StubUnauthorizedPlay(rejectedAccessToken: "stale-access-token");
        spotify.StubPlay("renewed-access-token");
        spotify.StubUserRefreshGrant("renewed-access-token", "renewed-refresh-token");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        // A token that is still valid by its expiry: the provider uses it as-is, Spotify
        // rejects it anyway (revoked), and only the 401 drives the renewal.
        using var client = await SignedInClientAsync(factory, accessToken: "stale-access-token", expiresIn: 3600);

        var play = await client.PutAsJsonAsync("/api/spotify/playback/play", new { spotifyAlbumId = AlbumId, deviceId = "device1" });

        // A 401 from Spotify is a token renewal, not a user error: the call succeeds after one retry.
        Assert.Equal(HttpStatusCode.NoContent, play.StatusCode);
        Assert.Equal(2, spotify.Server.LogEntries.Count(l => l.RequestMessage.Path == "/v1/me/player/play"));
        Assert.Single(spotify.Server.LogEntries, l =>
            l.RequestMessage.Path == "/api/token" && l.RequestMessage.Body!.Contains("grant_type=refresh_token", StringComparison.Ordinal));

        // The rotated refresh token was persisted.
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
        var row = await context.SpotifyUserSessions.SingleAsync();
        Assert.Equal("renewed-access-token", row.AccessToken);
        Assert.Equal("renewed-refresh-token", row.RefreshToken);
    }

    [Fact]
    public async Task Devices_WhenSpotifyAnswers401_EveryTime_EndsAsAnExpiredSession()
    {
        spotify.Reset();
        spotify.StubUnauthorizedDevices(exceptAccessToken: "renewed-access-token");
        spotify.StubUserRefreshGrant("renewed-access-token");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory, accessToken: "stale-access-token", expiresIn: 1);

        var response = await client.GetAsync("/api/spotify/devices");

        // The token is renewed before the call, so a 401 here means the session itself is gone:
        // the user is told to sign in again instead of seeing a raw Spotify error.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal("Your Spotify session has expired.", problem.Title);
    }

    [Fact]
    public async Task PlayerCalls_WhenNotSignedIn_TellTheUserToSignIn()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = NoRedirectClient(factory);

        foreach (var path in new[] { "/api/spotify/devices", "/api/spotify/playback" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("Sign in with Spotify first.", (await ReadProblemAsync(response)).Title);
        }

        var play = await client.PutAsJsonAsync("/api/spotify/playback/play", new { spotifyAlbumId = AlbumId, deviceId = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, play.StatusCode);
        Assert.Equal("Sign in with Spotify first.", (await ReadProblemAsync(play)).Title);
    }

    [Fact]
    public async Task Session_ReportsSignedOutByDefault()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = NoRedirectClient(factory);

        var session = await client.GetFromJsonAsync<SpotifySessionResponse>("/api/spotify/me");

        Assert.NotNull(session);
        Assert.False(session!.IsConnected);
        Assert.Null(session.SpotifyUserId);
    }

    [Fact]
    public async Task Logout_ForgetsTheSignIn()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var logout = await client.PostAsync("/api/spotify/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var session = await client.GetFromJsonAsync<SpotifySessionResponse>("/api/spotify/me");
        Assert.False(session!.IsConnected);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
        Assert.Empty(await context.SpotifyUserSessions.ToListAsync());
    }

    [Fact]
    public async Task Play_WithAMalformedAlbumId_IsRejectedBeforeAnySpotifyCall()
    {
        spotify.Reset();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        using var client = await SignedInClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/spotify/playback/play", new { spotifyAlbumId = "not a spotify id", deviceId = "device1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(spotify.Server.LogEntries, l => l.RequestMessage.Path.StartsWith("/v1/me/player", StringComparison.Ordinal));
    }

    /* ---- helpers ---- */

    /// <summary>
    /// A client that keeps cookies (the session and OAuth handshake cookies are set by the API)
    /// but does not follow the sign-in redirect, so its Location can be asserted.
    /// </summary>
    private static HttpClient NoRedirectClient(KatalogApiFactory factory)
        => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    /// <summary>
    /// Starts a sign-in and returns the CSRF state from the authorization URL together with the
    /// PKCE verifier the API kept in its HttpOnly handshake cookie. Both are needed to drive a
    /// callback that the API accepts.
    /// </summary>
    private static async Task<(string State, string Verifier)?> BeginSignInAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/spotify/auth/login");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var state = ParseQuery(Assert.Single(response.Headers.GetValues("Location")))["state"];
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("katalog_oauth=", StringComparison.Ordinal));
        var payload = Uri.UnescapeDataString(cookie["katalog_oauth=".Length..].Split(';')[0]);
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

        // The cookie holds "<state>.<codeVerifier>".
        var separator = decoded.IndexOf('.', StringComparison.Ordinal);
        return (decoded[..separator], decoded[(separator + 1)..]);
    }

    /// <summary>
    /// A client that has completed a real sign-in round trip against WireMock, so the stored
    /// tokens are exactly what the callback would have written.
    /// </summary>
    private async Task<HttpClient> SignedInClientAsync(KatalogApiFactory factory,
        string accessToken = "user-access-token", int expiresIn = 3600)
    {
        var client = NoRedirectClient(factory);
        var state = (await BeginSignInAsync(client))!.Value.State;
        spotify.StubUserCodeExchange(accessToken, "user-refresh-token", expiresIn);
        spotify.StubProfile();

        var callback = await client.GetAsync($"/api/spotify/auth/callback?code=the-code&state={HttpUtility.UrlEncode(state)}");
        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Contains("spotify=connected", callback.Headers.Location!.ToString(), StringComparison.Ordinal);
        return client;
    }

    private static Dictionary<string, string> ParseQuery(string url)
    {
        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        return query.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => query[k] ?? string.Empty);
    }

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
}
