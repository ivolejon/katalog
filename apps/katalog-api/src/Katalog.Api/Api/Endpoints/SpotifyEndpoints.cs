using Katalog.Api.Api.Filters;
using Katalog.Api.Api.Validators;
using Katalog.Api.Contracts;
using Katalog.Api.Features.SpotifyConnect;
using Katalog.Api.Infrastructure.Spotify.User;

namespace Katalog.Api.Api.Endpoints;

/// <summary>
/// Spotify Connect: sign in with a Spotify account, list that account's devices and play or
/// pause a release on one of them. These endpoints run with the signed-in user's own token
/// (kept server-side), unlike the catalog endpoints which use the app's client credentials.
/// </summary>
public static class SpotifyEndpoints
{
    public static RouteGroupBuilder MapSpotifyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/spotify");

        // GET /api/spotify/me - is a Spotify account connected for this browser?
        group.MapGet("/me", GetMe)
            .WithName("GetSpotifySession")
            .Produces<SpotifySessionResponse>();

        // GET /api/spotify/auth/login - 302 to accounts.spotify.com (authorization code + PKCE).
        group.MapGet("/auth/login", Login)
            .WithName("StartSpotifyLogin")
            .Produces(StatusCodes.Status302Found);

        // GET /api/spotify/auth/callback - Spotify's redirect target: exchanges the code and
        // sends the browser back to the app. Never returns tokens to the browser.
        group.MapGet("/auth/callback", Callback)
            .WithName("CompleteSpotifyLogin")
            .Produces(StatusCodes.Status302Found);

        group.MapPost("/auth/logout", Logout)
            .WithName("LogoutSpotify")
            .Produces(StatusCodes.Status204NoContent);

        // GET /api/spotify/devices - the user's Spotify Connect devices.
        group.MapGet("/devices", Devices)
            .WithName("GetSpotifyDevices")
            .Produces<SpotifyDevicesResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/spotify/playback - what is playing, for the play/pause toggle.
        group.MapGet("/playback", PlaybackState)
            .WithName("GetSpotifyPlaybackState")
            .Produces<SpotifyPlaybackStateResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        // PUT /api/spotify/playback/play - play a release on the chosen device.
        group.MapPut("/playback/play", Play)
            .WithName("PlayReleaseOnDevice")
            .AddEndpointFilter<ValidationFilter<PlayReleaseOnDeviceRequest>>()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // PUT /api/spotify/playback/pause - pause on the chosen device.
        group.MapPut("/playback/pause", Pause)
            .WithName("PausePlaybackOnDevice")
            .AddEndpointFilter<ValidationFilter<PausePlaybackRequest>>()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetMe(GetSpotifySession getSession, CancellationToken cancellationToken)
        => TypedResults.Ok(await getSession.GetAsync(cancellationToken));

    private static IResult Login(StartSpotifyLogin login)
        => TypedResults.Redirect(login.Start());

    private static async Task<IResult> Callback(CompleteSpotifyLogin completeLogin,
        [AsParameters] SpotifyCallbackRequest request, CancellationToken cancellationToken)
    {
        var target = await completeLogin.CompleteAsync(request.Code, request.State, request.Error, cancellationToken);
        return TypedResults.Redirect(target.ToString());
    }

    private static async Task<IResult> Logout(CompleteSpotifyLogin completeLogin, CancellationToken cancellationToken)
    {
        // 204 whether or not there was a session: the browser ends up signed out either way.
        await completeLogin.SignOutAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Devices(GetSpotifyDevices getDevices, CancellationToken cancellationToken)
        => await Handle(async () => TypedResults.Ok(await getDevices.ListAsync(cancellationToken)), cancellationToken);

    private static async Task<IResult> PlaybackState(ControlSpotifyPlayback playback,
        CancellationToken cancellationToken)
        => await Handle(async () => TypedResults.Ok(await playback.GetStateAsync(cancellationToken)), cancellationToken);

    private static async Task<IResult> Play(ControlSpotifyPlayback playback, PlayReleaseOnDeviceRequest request,
        CancellationToken cancellationToken)
        => await Handle(async () =>
        {
            await playback.PlayAsync(request.SpotifyAlbumId, Normalize(request.DeviceId), cancellationToken);
            return TypedResults.NoContent();
        }, cancellationToken);

    private static async Task<IResult> Pause(ControlSpotifyPlayback playback, PausePlaybackRequest request,
        CancellationToken cancellationToken)
        => await Handle(async () =>
        {
            await playback.PauseAsync(Normalize(request.DeviceId), cancellationToken);
            return TypedResults.NoContent();
        }, cancellationToken);

    /// <summary>Runs the endpoint body, mapping the expected Spotify user errors to problem details.</summary>
    private static async Task<IResult> Handle(Func<Task<IResult>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is SpotifyNotConnectedException
                                       or SpotifySessionExpiredException
                                       or SpotifyPlaybackException)
        {
            return SpotifyConnectErrors.ToProblem(ex);
        }
    }

    /// <summary>A blank device id means "Spotify's active device", not "the empty device".</summary>
    private static string? Normalize(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
}
