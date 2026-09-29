using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Infrastructure.Spotify.User;
using Microsoft.AspNetCore.Mvc;

namespace Katalog.Api.Api.Endpoints;

/// <summary>
/// Turns the Spotify Connect failures that are really the user's situation into problem details
/// the frontend can render: no active device, a missing Premium subscription, an expired sign-in
/// or a device Spotify will not accept commands on. A 401 from Spotify is never surfaced as a
/// raw error - the token has already been renewed and retried once by then, so it means the
/// sign-in itself is gone and the user must sign in again.
/// </summary>
internal static class SpotifyConnectErrors
{
    public static IResult ToProblem(Exception exception) => exception switch
    {
        SpotifyNotConnectedException => TypedResults.Problem(
            title: "Sign in with Spotify first.",
            detail: "Connect a Spotify account to play releases on your own device.",
            statusCode: StatusCodes.Status401Unauthorized),

        SpotifySessionExpiredException expired => TypedResults.Problem(
            title: "Your Spotify session has expired.",
            detail: expired.Message,
            statusCode: StatusCodes.Status401Unauthorized),

        SpotifyPlaybackException playback => ToPlaybackProblem(playback),

        // Anything else (including the app-credentials client) is a genuine failure.
        _ => throw exception,
    };

    private static IResult ToPlaybackProblem(SpotifyPlaybackException playback) => playback.Reason switch
    {
        // No device is active and the call carried no usable device id.
        "NO_ACTIVE_DEVICE" => Problem(
            StatusCodes.Status404NotFound,
            "No active Spotify device.",
            "Open the Spotify app on the device you want to hear, or pick another device.",
            playback.Reason),
        "RESTRICTED_DEVICE" => Problem(
            StatusCodes.Status403Forbidden,
            "That Spotify device cannot be controlled.",
            "Spotify does not accept playback commands on this device; choose another one.",
            playback.Reason),
        "PREMIUM_REQUIRED" => PremiumProblem(playback),
        _ => playback.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => Problem(
                StatusCodes.Status404NotFound,
                "No active Spotify device.",
                "Open the Spotify app on the device you want to hear, or pick another device.",
                playback.Reason),
            System.Net.HttpStatusCode.Forbidden => PremiumProblem(playback),
            System.Net.HttpStatusCode.Unauthorized => Problem(
                StatusCodes.Status401Unauthorized,
                "Your Spotify session has expired.",
                "Sign in with Spotify again to keep controlling your devices."),
            _ => Problem(
                StatusCodes.Status502BadGateway,
                "Spotify could not start playback.",
                playback.Message),
        }
    };

    /// <summary>Playback control is a Premium feature; the Web API answers 403 for free accounts.</summary>
    private static IResult PremiumProblem(SpotifyPlaybackException playback) => Problem(
        StatusCodes.Status403Forbidden,
        "Spotify Premium is required.",
        "Controlling playback from Katalog needs a Spotify Premium account.",
        playback.Reason);

    private static IResult Problem(int statusCode, string title, string detail, string? reason = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        };

        if (reason is not null)
        {
            problem.Extensions["reason"] = reason;
        }

        return TypedResults.Problem(problem);
    }
}
