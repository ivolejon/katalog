namespace Katalog.Api.Contracts;

/// <summary>
/// Spotify sign-in status for this browser. Carries no tokens: only what the UI needs to show
/// who is connected and to gate the play buttons.
/// </summary>
public sealed record SpotifySessionResponse(
    bool IsConnected,
    string? SpotifyUserId,
    string? DisplayName,
    /// <summary>"premium" or "free" as reported by Spotify; the player endpoints need Premium.</summary>
    string? Product);

/// <summary>A Spotify Connect device the user can play on.</summary>
public sealed record SpotifyDeviceResponse(
    string Id,
    string Name,
    /// <summary>Device type as reported by Spotify ("computer", "smartphone", "speaker", ...).</summary>
    string Type,
    bool IsActive,
    /// <summary>Spotify refuses Web API commands on a restricted device, so the UI disables it.</summary>
    bool IsRestricted);

/// <summary>The user's devices plus the one Spotify currently considers active.</summary>
public sealed record SpotifyDevicesResponse(
    IReadOnlyList<SpotifyDeviceResponse> Devices,
    string? ActiveDeviceId);

/// <summary>What is playing right now, for the play/pause toggle on a release.</summary>
public sealed record SpotifyPlaybackStateResponse(
    bool IsPlaying,
    /// <summary>Spotify URI of the playing context, e.g. "spotify:album:&lt;id&gt;"; null when track-based.</summary>
    string? ContextUri,
    string? ContextType,
    string? DeviceId);

/// <summary>Play a release on a Spotify device. The album id is the release's stored Spotify id.</summary>
public sealed record PlayReleaseOnDeviceRequest(string SpotifyAlbumId, string? DeviceId);

/// <summary>Pause playback on a device.</summary>
public sealed record PausePlaybackRequest(string? DeviceId);
