using System.Net;

namespace Katalog.Api.Infrastructure.Spotify;

/// <summary>
/// Raised when a Spotify API/SDK call fails in a way that is not a normal HTTP response.
/// </summary>
public sealed class SpotifyApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
