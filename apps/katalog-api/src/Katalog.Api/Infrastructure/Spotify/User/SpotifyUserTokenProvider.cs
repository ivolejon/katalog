using Katalog.Api.Domain;
using Katalog.Api.Setup;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Infrastructure.Spotify.User;

/// <summary>
/// Supplies the caller's own Spotify access token (authorization code flow, PKCE) and renews
/// it with the stored refresh token shortly before it expires. A refresh failure surfaces as
/// <see cref="SpotifySessionExpiredException"/> so the caller is told to sign in again rather
/// than being handed a raw Spotify error. Nothing here logs a token value.
/// </summary>
public sealed class SpotifyUserTokenProvider(
    SpotifyUserSessionStore sessionStore,
    IHttpClientFactory httpClientFactory,
    IOptions<SpotifyOptions> options,
    TimeProvider timeProvider,
    ILogger<SpotifyUserTokenProvider> logger)
{
    /// <summary>Renew this long before the token actually expires.</summary>
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>Valid access token for this browser's Spotify sign-in, renewed when stale.</summary>
    /// <exception cref="SpotifyNotConnectedException">Spotify is not connected for this session.</exception>
    /// <exception cref="SpotifySessionExpiredException">The stored session could not be renewed.</exception>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(cancellationToken);
        if (timeProvider.GetUtcNow() < session.ExpiresAtUtc - ExpirySkew)
            return session.AccessToken;

        return await RenewAsync(session, skipCacheCheck: false, cancellationToken);
    }

    /// <summary>
    /// Renews unconditionally. Used for the one "refresh + retry" on a 401 from Spotify, which
    /// means the access token was rejected rather than merely expired - so the still-valid
    /// stored token must not be reused, or the retry would send the same rejected token.
    /// </summary>
    public async Task<string> ForceRefreshAsync(CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(cancellationToken);
        return await RenewAsync(session, skipCacheCheck: true, cancellationToken);
    }

    private async Task<SpotifyUserSession> RequireSessionAsync(CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetAsync(cancellationToken);
        return session ?? throw new SpotifyNotConnectedException();
    }

    private async Task<string> RenewAsync(SpotifyUserSession session, bool skipCacheCheck,
        CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have renewed already; re-read to avoid a
            // pointless exchange (and a needless rotation of the refresh token). A forced
            // renewal skips this: it exists precisely because the stored token was rejected.
            var current = await sessionStore.GetAsync(cancellationToken) ?? session;
            if (!skipCacheCheck && !ReferenceEquals(current, session)
                && timeProvider.GetUtcNow() < current.ExpiresAtUtc - ExpirySkew)
            {
                return current.AccessToken;
            }

            using var client = httpClientFactory.CreateClient(SpotifyClientNames.Accounts);
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = current.RefreshToken,
                    ["client_id"] = options.Value.ClientId,
                })
            };

            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<SpotifyUserTokenResponse>(cancellationToken);
            if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                logger.LogWarning("Renewing the Spotify sign-in failed with status {StatusCode}.", (int)response.StatusCode);
                throw new SpotifySessionExpiredException(
                    "The Spotify session could not be renewed. Please sign in again.");
            }

            // Spotify rotates the refresh token on most renewals; keep whichever came back.
            var refreshToken = string.IsNullOrWhiteSpace(payload.RefreshToken) ? current.RefreshToken : payload.RefreshToken;
            var expiresAt = timeProvider.GetUtcNow() + TimeSpan.FromSeconds(payload.ExpiresIn);
            await sessionStore.UpdateTokensAsync(current, payload.AccessToken, refreshToken, expiresAt, payload.Scope,
                cancellationToken);

            logger.LogDebug("Renewed the Spotify sign-in, valid for {ExpiresIn}s.", payload.ExpiresIn);
            return payload.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}

/// <summary>Token endpoint response for the authorization-code and refresh-token grants.</summary>
public sealed record SpotifyUserTokenResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
    [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
    [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string? Scope = null);
