using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Katalog.Api.Infrastructure.Spotify.User;

/// <summary>Spotify Connect player calls, made with the signed-in user's own access token.</summary>
public interface ISpotifyPlaybackClient
{
    /// <summary>GET /v1/me/player/devices - the devices the user can play on.</summary>
    Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// GET /v1/me/player - current playback state, or null when Spotify answers 204
    /// ("playback not available or active"). Needed so a play/pause toggle can reflect what is
    /// really playing instead of assuming.
    /// </summary>
    Task<SpotifyPlaybackState?> GetPlaybackStateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// PUT /v1/me/player/play with the album as playback context. The Spotify URI is built from
    /// the already stored album id, so no album lookup is needed, and device_id travels as a
    /// query parameter (the published spec defines it there for both play and pause).
    /// </summary>
    Task PlayAlbumAsync(string spotifyAlbumId, string? deviceId, CancellationToken cancellationToken);

    /// <summary>PUT /v1/me/player/pause, targeted at the same context's device.</summary>
    Task PauseAsync(string? deviceId, CancellationToken cancellationToken);
}

public sealed class SpotifyPlaybackClient(HttpClient httpClient) : ISpotifyPlaybackClient
{
    public async Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("v1/me/player/devices", cancellationToken);
        await EnsureSuccessAsync(response, "list Spotify devices", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<SpotifyDevicesPage>(cancellationToken);
        return payload?.Devices ?? [];
    }

    public async Task<SpotifyPlaybackState?> GetPlaybackStateAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("v1/me/player", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        await EnsureSuccessAsync(response, "read the Spotify playback state", cancellationToken);
        return await response.Content.ReadFromJsonAsync<SpotifyPlaybackState>(cancellationToken);
    }

    public async Task PlayAlbumAsync(string spotifyAlbumId, string? deviceId, CancellationToken cancellationToken)
    {
        var path = DeviceQuery("v1/me/player/play", deviceId);
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(new { context_uri = $"spotify:album:{spotifyAlbumId}" }),
        };

        var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "start Spotify playback", cancellationToken);
    }

    public async Task PauseAsync(string? deviceId, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsync(DeviceQuery("v1/me/player/pause", deviceId), null, cancellationToken);
        await EnsureSuccessAsync(response, "pause Spotify playback", cancellationToken);
    }

    /// <summary>Device id is an optional query parameter; without it Spotify uses the active device.</summary>
    private static string DeviceQuery(string path, string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? path : $"{path}?device_id={Uri.EscapeDataString(deviceId)}";

    /// <summary>
    /// Turns a Spotify error response into a <see cref="SpotifyPlaybackException"/> carrying
    /// Spotify's reason, so the API can answer with a message the UI shows (no active device,
    /// Premium required, restricted device) instead of a bare failure.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var error = await ReadErrorAsync(response, cancellationToken);
        response.Dispose();

        throw new SpotifyPlaybackException(response.StatusCode, error?.Reason, error?.Message ?? action);
    }

    private static async Task<SpotifyErrorBody?> ReadErrorAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<SpotifyErrorResponse>(cancellationToken))?.Error;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

public sealed record SpotifyDevicesPage(
    [property: JsonPropertyName("devices")] IReadOnlyList<SpotifyDevice> Devices);

/// <summary>A Spotify Connect device (DeviceObject in the published schema).</summary>
public sealed record SpotifyDevice(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("is_private_session")] bool IsPrivateSession,
    [property: JsonPropertyName("is_restricted")] bool IsRestricted,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("volume_percent")] int? VolumePercent,
    [property: JsonPropertyName("supports_volume")] bool SupportsVolume = false);

/// <summary>
/// The subset of the playback state Katalog needs: what is playing, where, and on which device.
/// <c>context</c> is null when playback is track-based rather than album/playlist based.
/// </summary>
public sealed record SpotifyPlaybackState(
    [property: JsonPropertyName("is_playing")] bool IsPlaying,
    [property: JsonPropertyName("context")] SpotifyPlaybackContext? Context,
    [property: JsonPropertyName("device")] SpotifyDevice? Device);

public sealed record SpotifyPlaybackContext(
    [property: JsonPropertyName("uri")] string? Uri,
    [property: JsonPropertyName("type")] string? Type);

public sealed record SpotifyErrorResponse(
    [property: JsonPropertyName("error")] SpotifyErrorBody Error);

public sealed record SpotifyErrorBody(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("reason")] string? Reason);
