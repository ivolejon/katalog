using FluentValidation;
using Katalog.Api.Contracts;

namespace Katalog.Api.Api.Validators;

/// <summary>
/// Query binding for GET /api/spotify/auth/callback, Spotify's redirect target. All three
/// parameters are optional: a user who declines the consent screen comes back with only
/// <c>error</c>, which the callback reports back to the app.
/// </summary>
public sealed class SpotifyCallbackRequest
{
    public string? Code { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
}

public sealed class PlayReleaseOnDeviceValidator : AbstractValidator<PlayReleaseOnDeviceRequest>
{
    public PlayReleaseOnDeviceValidator()
    {
        RuleFor(v => v.SpotifyAlbumId)
            .NotEmpty()
            .WithMessage("SpotifyAlbumId is required.")
            .Matches("^[A-Za-z0-9]{6,64}$")
            .WithMessage("SpotifyAlbumId must be an alphanumeric Spotify id.");

        RuleFor(v => v.DeviceId)
            .Must(id => id is null || (id.Trim().Length > 0 && id.Trim().Length <= 128))
            .WithMessage("DeviceId must be a Spotify device id.");
    }
}

public sealed class PausePlaybackRequestValidator : AbstractValidator<PausePlaybackRequest>
{
    public PausePlaybackRequestValidator()
    {
        RuleFor(v => v.DeviceId)
            .Must(id => id is null || (id.Trim().Length > 0 && id.Trim().Length <= 128))
            .WithMessage("DeviceId must be a Spotify device id.");
    }
}
