using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure.Spotify.User;

namespace Katalog.Api.Features.SpotifyConnect;

/// <summary>
/// The user's Spotify Connect devices, with the currently active one marked so the frontend can
/// preselect it. A device Spotify reports as restricted cannot accept Web API commands, so the
/// response says so and the UI can disable it instead of letting the user pick a dead end.
/// </summary>
public sealed class GetSpotifyDevices(ISpotifyPlaybackClient playbackClient)
{
    public async Task<SpotifyDevicesResponse> ListAsync(CancellationToken cancellationToken)
    {
        var devices = await playbackClient.GetDevicesAsync(cancellationToken);
        var response = devices
            .Where(device => !string.IsNullOrWhiteSpace(device.Id))
            .Select(device => new SpotifyDeviceResponse(
                device.Id!,
                device.Name ?? "Unknown device",
                device.Type ?? "Unknown",
                device.IsActive,
                device.IsRestricted))
            .ToList();

        return new SpotifyDevicesResponse(response, response.FirstOrDefault(d => d.IsActive)?.Id);
    }
}
