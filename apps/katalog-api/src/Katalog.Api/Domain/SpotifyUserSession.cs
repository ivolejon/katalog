namespace Katalog.Api.Domain;

/// <summary>
/// A signed-in Spotify account together with its OAuth tokens, stored server-side (Spotify
/// Connect control, arch report §3.3). One row per Spotify account: <see cref="Id"/> is the
/// anonymous browser session that completed the sign-in (the app has no user accounts in the
/// MVP), and the row is looked up by that session id on every call. Signing in again with the
/// same Spotify account re-points the row at the new session, so tokens never travel to the
/// browser and are never exposed in an API response.
/// </summary>
public sealed class SpotifyUserSession
{
    /// <summary>Anonymous browser session id (uuidv7) that owns this Spotify sign-in.</summary>
    public Guid Id { get; set; }

    /// <summary>Spotify account id (GET /v1/me).</summary>
    public string SpotifyUserId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    /// <summary>Spotify product the account has ("premium" / "free"); player calls need Premium.</summary>
    public string? Product { get; set; }

    /// <summary>OAuth access token. Never logged, never returned in an API response.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>OAuth refresh token. Never logged, never returned.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Space-separated granted scopes, kept for diagnostics only.</summary>
    public string? Scope { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
