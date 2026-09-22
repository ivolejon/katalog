using FluentValidation;
using Katalog.Api.Contracts;

namespace Katalog.Api.Api.Validators;

public sealed class CreateLabelValidator : AbstractValidator<CreateLabelRequest>
{
    public CreateLabelValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name must be at most 100 characters.")
            // The slug derives from the name; a name with no letters/digits would produce an
            // empty slug and break the unique slug contract.
            .Must(name => string.IsNullOrEmpty(name) || name.Any(char.IsLetterOrDigit))
            .WithMessage("Name must contain at least one letter or digit.");
    }
}

public sealed class UpdateLabelValidator : AbstractValidator<UpdateLabelRequest>
{
    public UpdateLabelValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name must be at most 100 characters.")
            .Must(name => string.IsNullOrEmpty(name) || name.Any(char.IsLetterOrDigit))
            .WithMessage("Name must contain at least one letter or digit.");
    }
}

public sealed class AddArtistToLabelValidator : AbstractValidator<AddArtistToLabelRequest>
{
    public AddArtistToLabelValidator()
    {
        RuleFor(v => v.SpotifyArtistId)
            .NotEmpty()
            .WithMessage("SpotifyArtistId is required.")
            .Matches("^[A-Za-z0-9]{6,64}$")
            .WithMessage("SpotifyArtistId must be an alphanumeric Spotify id.");
    }
}
