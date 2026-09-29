using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure.Spotify.User;

namespace Katalog.Api.Features.SpotifyConnect;

/// <summary>
/// Spotify Connect playback control for a release: start an album as the playback context on the
/// user's own device, pause it, and read the current state so a toggle can show what is really
/// playing. The album's Spotify id is all that is needed - it is already stored with the
/// release, so no album lookup happens here (Spotify Web API has no streaming, this only steers
/// a device the user already plays on).
/// </summary>
public sealed class ControlSpotifyPlayback(ISpotifyPlaybackClient playbackClient)
{
    public async Task PlayAsync(string spotifyAlbumId, string? deviceId, CancellationToken cancellationToken)
        => await playbackClient.PlayAlbumAsync(spotifyAlbumId, deviceId, cancellationToken);

    public async Task PauseAsync(string? deviceId, CancellationToken cancellationToken)
        => await playbackClient.PauseAsync(deviceId, cancellationToken);

    /// <summary>
    /// Current playback state, or a stopped state when Spotify answers 204 (nothing is playing
    /// anywhere). The context URI is what the frontend matches against the release it is toggling.
    /// </summary>
    public async Task<SpotifyPlaybackStateResponse> GetStateAsync(CancellationToken cancellationToken)
    {
        var state = await playbackClient.GetPlaybackStateAsync(cancellationToken);
        if (state is null)
            return new SpotifyPlaybackStateResponse(false, null, null, null);

        return new SpotifyPlaybackStateResponse(
            state.IsPlaying,
            state.Context?.Uri,
            state.Context?.Type,
            string.IsNullOrWhiteSpace(state.Device?.Id) ? null : state.Device!.Id);
    }
}
