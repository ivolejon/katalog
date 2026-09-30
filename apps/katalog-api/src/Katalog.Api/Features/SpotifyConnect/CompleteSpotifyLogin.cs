using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Infrastructure.Spotify.User;
using Katalog.Api.Setup;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Features.SpotifyConnect;

/// <summary>
/// Completes the Spotify sign-in: proves the callback belongs to the sign-in this browser
/// started (state match + PKCE), exchanges the code, reads the profile and stores the tokens
/// server-side. The browser is then sent back to the app with the outcome as a query parameter -
/// it never sees a token.
/// </summary>
public sealed class CompleteSpotifyLogin(
    ISpotifyUserOAuthService oauthService,
    SpotifyUserSessionStore sessionStore,
    BrowserSession browserSession,
    IOptions<SpotifyOptions> options,
    TimeProvider timeProvider,
    ILogger<CompleteSpotifyLogin> logger)
{
    public const string ConnectedOutcome = "connected";
    public const string FailedOutcome = "failed";

    /// <summary>
    /// Finishes the sign-in and returns where to send the browser. An error is reported as a URL
    /// back to the app (not an error page) so a user who denies access or has a dead session
    /// lands back in the app with a readable reason.
    /// </summary>
    public async Task<Uri> CompleteAsync(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        // A denied consent screen comes back as ?error=access_denied with no code to exchange.
        if (!string.IsNullOrWhiteSpace(error))
            return RedirectToApp(FailedOutcome, error);

        var handshake = browserSession.TakeOAuthHandshake();
        if (handshake is null)
        {
            logger.LogInformation("A Spotify callback arrived without a matching sign-in attempt.");
            return RedirectToApp(FailedOutcome, "no_sign_in_attempt");
        }

        if (string.IsNullOrWhiteSpace(code) || !FixedTimeEquals(handshake.Value.State, state))
        {
            logger.LogInformation("A Spotify callback failed the state check.");
            return RedirectToApp(FailedOutcome, "state_mismatch");
        }

        try
        {
            var tokens = await oauthService.ExchangeCodeAsync(code, handshake.Value.CodeVerifier, cancellationToken);
            var profile = await oauthService.GetProfileAsync(tokens.AccessToken, cancellationToken);

            await sessionStore.SaveAsync(
                profile.Id,
                profile.DisplayName,
                profile.Product,
                tokens.AccessToken,
                tokens.RefreshToken,
                timeProvider.GetUtcNow() + tokens.ExpiresIn,
                tokens.Scope,
                cancellationToken);

            return RedirectToApp(ConnectedOutcome);
        }
        catch (Exception ex) when (ex is SpotifyLoginFailedException or SpotifyApiException or HttpRequestException)
        {
            logger.LogWarning(ex, "Completing the Spotify sign-in failed.");
            return RedirectToApp(FailedOutcome, "token_exchange_failed");
        }
    }

    /// <summary>Ends this browser's Spotify sign-in and forgets the stored tokens.</summary>
    public Task<bool> SignOutAsync(CancellationToken cancellationToken) => sessionStore.DeleteAsync(cancellationToken);

    private Uri RedirectToApp(string outcome, string? reason = null)
    {
        var baseUrl = options.Value.WebBaseUrl.TrimEnd('/');
        var query = reason is null
            ? $"?spotify={outcome}"
            : $"?spotify={outcome}&reason={Uri.EscapeDataString(reason)}";

        return new Uri($"{baseUrl}/{query}");
    }

    /// <summary>Constant-time comparison so the state check does not leak by timing.</summary>
    private static bool FixedTimeEquals(string expected, string? actual)
        => actual is not null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(actual));
}
