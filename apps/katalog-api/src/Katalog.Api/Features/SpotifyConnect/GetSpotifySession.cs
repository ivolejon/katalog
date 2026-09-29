using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure.Spotify.User;

namespace Katalog.Api.Features.SpotifyConnect;

/// <summary>
/// Spotify sign-in status for this browser: whether an account is connected and who it is.
/// Deliberately reports no tokens - only the id, display name and product the UI needs.
/// </summary>
public sealed class GetSpotifySession(SpotifyUserSessionStore sessionStore)
{
    public async Task<SpotifySessionResponse> GetAsync(CancellationToken cancellationToken)
    {
        var session = await sessionStore.GetAsync(cancellationToken);
        if (session is null)
            return new SpotifySessionResponse(false, null, null, null);

        return new SpotifySessionResponse(true, session.SpotifyUserId, session.DisplayName, session.Product);
    }
}
