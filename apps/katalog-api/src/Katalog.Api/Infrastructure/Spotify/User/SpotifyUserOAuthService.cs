using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Katalog.Api.Setup;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Infrastructure.Spotify.User;

/// <summary>
/// The two halves of the Spotify Connect sign-in that do not need a stored session: building the
/// authorization request and exchanging the returned code. Kept apart from the playback client
/// because the callback has no access token until the exchange has happened.
/// </summary>
public interface ISpotifyUserOAuthService
{
    /// <summary>
    /// The accounts.spotify.com authorization URL for the authorization code flow with PKCE
    /// (S256). Scopes cover reading and modifying playback state only - no streaming scope,
    /// because the Web API cannot stream audio.
    /// </summary>
    /// <remarks>
    /// Returned as a string, not a <see cref="Uri"/>: a scope contains a space, and
    /// <c>Uri.ToString()</c> unescapes %20, which would put a literal space in the Location
    /// header and make Spotify read only the first scope.
    /// </remarks>
    string BuildAuthorizeUrl(string state, string codeChallenge);

    /// <summary>Exchanges an authorization code for the user's tokens (code_verifier proves it is ours).</summary>
    Task<SpotifyUserTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken);

    /// <summary>Reads the signed-in account's id, display name and product (premium/free).</summary>
    Task<SpotifyUserProfile> GetProfileAsync(string accessToken, CancellationToken cancellationToken);
}

public sealed record SpotifyUserTokens(string AccessToken, string RefreshToken, TimeSpan ExpiresIn, string? Scope);

public sealed record SpotifyUserProfile(string Id, string? DisplayName, string? Product);

public sealed class SpotifyUserOAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<SpotifyOptions> options,
    ILogger<SpotifyUserOAuthService> logger) : ISpotifyUserOAuthService
{
    public string BuildAuthorizeUrl(string state, string codeChallenge)
    {
        // The scope is space-separated as Spotify documents it; every value is URL-encoded.
        var parameters = new (string Key, string Value)[]
        {
            ("client_id", options.Value.ClientId),
            ("response_type", "code"),
            ("redirect_uri", options.Value.RedirectUri),
            ("scope", SpotifyOptions.PlaybackStateScope),
            ("state", state),
            ("code_challenge_method", "S256"),
            ("code_challenge", codeChallenge),
        };

        var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{options.Value.AccountsBaseUrl.TrimEnd('/')}/authorize?{query}";
    }

    public async Task<SpotifyUserTokens> ExchangeCodeAsync(string code, string codeVerifier,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(SpotifyClientNames.Accounts);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = options.Value.RedirectUri,
                ["client_id"] = options.Value.ClientId,
                ["code_verifier"] = codeVerifier,
            })
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<SpotifyUserTokenResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.AccessToken)
            || string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            // The body may echo the code or tokens, so it is deliberately not logged.
            logger.LogWarning("The Spotify authorization code exchange failed with status {StatusCode}.",
                (int)response.StatusCode);
            throw new SpotifyLoginFailedException("Spotify rejected the sign-in (the code may have expired).");
        }

        return new SpotifyUserTokens(
            payload.AccessToken,
            payload.RefreshToken!,
            TimeSpan.FromSeconds(payload.ExpiresIn),
            payload.Scope);
    }

    public async Task<SpotifyUserProfile> GetProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(SpotifyClientNames.Profile);
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Reading the Spotify profile failed with status {StatusCode}.", (int)response.StatusCode);
            throw new SpotifyLoginFailedException("Spotify did not return a profile for this sign-in.");
        }

        var profile = await response.Content.ReadFromJsonAsync<SpotifyProfileResponse>(cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id))
            throw new SpotifyLoginFailedException("Spotify did not return a profile for this sign-in.");

        return new SpotifyUserProfile(profile.Id, profile.DisplayName, profile.Product);
    }
}

public sealed record SpotifyProfileResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("product")] string? Product);
