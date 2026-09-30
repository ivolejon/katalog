using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Infrastructure.Spotify.User;

/// <summary>
/// Reads and writes the single Spotify sign-in row belonging to this browser session
/// (see <see cref="SpotifyUserSession"/>). Tokens are only ever handled here and by
/// <see cref="SpotifyUserTokenProvider"/>; nothing in this class returns them to a caller.
/// </summary>
public sealed class SpotifyUserSessionStore(
    KatalogContext context,
    BrowserSession browserSession,
    TimeProvider timeProvider)
{
    /// <summary>The sign-in for this browser, or null when Spotify is not connected.</summary>
    public async Task<SpotifyUserSession?> GetAsync(CancellationToken cancellationToken)
    {
        var sessionId = browserSession.GetOrCreateSessionId();
        return await context.SpotifyUserSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    /// <summary>
    /// Stores the tokens from a completed authorization-code exchange, replacing any row that
    /// already exists for the same Spotify account (one row per account) and clearing the
    /// stored tokens of any other session that had signed in with it.
    /// </summary>
    public async Task<SpotifyUserSession> SaveAsync(string spotifyUserId, string? displayName, string? product,
        string accessToken, string refreshToken, DateTimeOffset expiresAtUtc, string? scope,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sessionId = browserSession.GetOrCreateSessionId();

        // Replace the row for this Spotify account and the current browser's row: one row per
        // account, and a fresh sign-in re-points the account at the session doing the sign-in
        // (a second browser taking the account over invalidates the first one's access).
        var stale = await context.SpotifyUserSessions
            .Where(s => s.SpotifyUserId == spotifyUserId || s.Id == sessionId)
            .ToListAsync(cancellationToken);
        context.SpotifyUserSessions.RemoveRange(stale);

        var session = new SpotifyUserSession
        {
            Id = sessionId,
            SpotifyUserId = spotifyUserId,
            DisplayName = displayName,
            Product = product,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = expiresAtUtc,
            Scope = scope,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        context.SpotifyUserSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        return session;
    }

    /// <summary>Persists a renewed access token (and the refresh token Spotify may have rotated).</summary>
    public async Task UpdateTokensAsync(SpotifyUserSession session, string accessToken, string refreshToken,
        DateTimeOffset expiresAtUtc, string? scope, CancellationToken cancellationToken)
    {
        var row = await context.SpotifyUserSessions
            .SingleOrDefaultAsync(s => s.Id == session.Id, cancellationToken);
        if (row is null)
            throw new SpotifySessionExpiredException("The Spotify sign-in for this session no longer exists.");

        row.AccessToken = accessToken;
        row.RefreshToken = refreshToken;
        row.ExpiresAtUtc = expiresAtUtc;
        if (!string.IsNullOrWhiteSpace(scope))
            row.Scope = scope;
        row.UpdatedAtUtc = timeProvider.GetUtcNow();

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ends this browser's Spotify sign-in. Returns whether a row was removed.</summary>
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken)
    {
        var sessionId = browserSession.GetOrCreateSessionId();
        var row = await context.SpotifyUserSessions
            .SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (row is null)
            return false;

        context.SpotifyUserSessions.Remove(row);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
