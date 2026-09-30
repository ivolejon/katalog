using System.Net;

namespace Katalog.Api.Infrastructure.Spotify.User;

/// <summary>The sign-in round trip could not be completed; the browser is sent back to the app.</summary>
public sealed class SpotifyLoginFailedException(string message) : Exception(message);

/// <summary>No Spotify account is signed in for this browser session.</summary>
public sealed class SpotifyNotConnectedException()
    : Exception("No Spotify account is connected for this session.");

/// <summary>
/// The stored session could not be renewed (revoked refresh token). The user must sign in again;
/// this is an expired session, not a server error.
/// </summary>
public sealed class SpotifySessionExpiredException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// A Spotify player call answered with an error that is the user's situation, not a bug:
/// no active device (404), missing Premium (403), an invalid/expired session (401) or a
/// restricted device (403). Carries the status and Spotify's machine-readable reason so the
/// API can answer with a message the UI can show as-is.
/// </summary>
public sealed class SpotifyPlaybackException(
    HttpStatusCode statusCode,
    string? reason,
    string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>Spotify's error reason, e.g. NO_ACTIVE_DEVICE / PREMIXTURE_REQUIRED / RESTRICTED_DEVICE.</summary>
    public string? Reason { get; } = reason;
}
