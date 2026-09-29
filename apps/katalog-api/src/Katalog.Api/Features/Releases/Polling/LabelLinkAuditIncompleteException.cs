namespace Katalog.Api.Features.Releases.Polling;

/// <summary>
/// Thrown when a label link audit cannot positively verify every link (Spotify unreachable,
/// an album GET failed, or an album reports no label). The audit is incomplete, so the caller
/// must not persist the state transition that required the audit (e.g. a label rename) and
/// must roll its transaction back.
/// </summary>
public sealed class LabelLinkAuditIncompleteException(Guid labelId, string spotifyAlbumId, string reason,
    Exception? innerException = null)
    : Exception(
        $"Link audit for label {labelId} is incomplete: album {spotifyAlbumId} could not be verified ({reason}).",
        innerException);
