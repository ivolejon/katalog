using Katalog.Api.Setup;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Infrastructure.Spotify;

/// <summary>
/// Provides the Spotify app token (client credentials flow) with an in-memory cache.
/// The token is cached until just before <c>expires_in</c> expiry and a semaphore serializes
/// refreshes so concurrent callers only ever trigger one token exchange (arch report §3.5).
/// <see cref="ForceRefreshAsync"/> invalidates the cache - used by <see cref="SpotifyTokenHandler"/>
/// when Spotify answers 401.
/// </summary>
public sealed class SpotifyTokenProvider(
    ILogger<SpotifyTokenProvider> logger,
    IOptions<SpotifyOptions> options,
    TimeProvider timeProvider,
    IHttpClientFactory httpClientFactory)
{
    /// <summary>Refresh skew: fetch a new token a bit before the old one actually expires.</summary>
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CachedToken? _cached;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var cached = _cached;
        if (cached is not null && timeProvider.GetUtcNow() < cached.ExpiresAtUtc - ExpirySkew)
            return cached.AccessToken;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            cached = _cached;
            if (cached is not null && timeProvider.GetUtcNow() < cached.ExpiresAtUtc - ExpirySkew)
                return cached.AccessToken;

            var token = await FetchTokenAsync(cancellationToken);
            _cached = new CachedToken(token.AccessToken, timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn));
            logger.LogDebug("Fetched new Spotify client credentials token, valid for {ExpiresIn}s.", token.ExpiresIn);
            return _cached.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Invalidates the cached token and fetches a fresh one. Used for the "one refresh + retry"
    /// dance on 401 responses (arch report §3.5).
    /// </summary>
    public async Task<string> ForceRefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            _cached = null;
            var token = await FetchTokenAsync(cancellationToken);
            _cached = new CachedToken(token.AccessToken, timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn));
            logger.LogDebug("Force-refreshed Spotify client credentials token.");
            return _cached.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<SpotifyTokenResponse> FetchTokenAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(SpotifyClientNames.Accounts);

        // Spotify accepts client_id/client_secret in the body for the client credentials flow,
        // but the documented form is Basic auth (research §2.6).
        var credentials = System.Text.Encoding.UTF8.GetBytes($"{options.Value.ClientId}:{options.Value.ClientSecret}");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(credentials));

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>(cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            logger.LogError("Spotify token exchange failed with status {StatusCode} and body {Body}",
                response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
            throw new SpotifyApiException($"Spotify token exchange failed with status {(int)response.StatusCode}.");
        }

        return payload;
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
