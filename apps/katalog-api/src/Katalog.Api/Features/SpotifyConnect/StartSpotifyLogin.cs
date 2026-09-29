using Katalog.Api.Infrastructure.Spotify.User;

namespace Katalog.Api.Features.SpotifyConnect;

/// <summary>
/// Starts the Spotify sign-in: mints a CSRF state plus a PKCE verifier, remembers them in a
/// short-lived HttpOnly cookie, and hands back the accounts.spotify.com authorization URL the
/// browser is redirected to. The app's client secret never leaves the backend.
/// </summary>
public sealed class StartSpotifyLogin(
    ISpotifyUserOAuthService oauthService,
    BrowserSession browserSession)
{
    public string Start()
    {
        var state = BrowserSession.CreateState();
        var (verifier, challenge) = BrowserSession.CreatePkce();

        browserSession.StoreOAuthHandshake(state, verifier);
        return oauthService.BuildAuthorizeUrl(state, challenge);
    }
}
