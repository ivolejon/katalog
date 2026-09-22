using FluentValidation;
using Katalog.Api.Contracts;
using Katalog.Api.Setup;

namespace Katalog.Api.Api.Validators;

/// <summary>Query binding for GET /api/search (bound from the query string via [AsParameters]).</summary>
public sealed class SearchArtistsRequest
{
    public string Q { get; set; } = string.Empty;
    public string? Type { get; set; }
    public int? Limit { get; set; }
}

public sealed class SearchArtistsValidator : AbstractValidator<SearchArtistsRequest>
{
    public SearchArtistsValidator()
    {
        RuleFor(v => v.Q)
            .NotEmpty()
            .WithMessage("Query parameter 'q' is required.")
            .MaximumLength(200)
            .WithMessage("Query parameter 'q' must be at most 200 characters.");

        RuleFor(v => v.Type)
            .Must(t => string.IsNullOrEmpty(t) || t == "artist")
            .WithMessage("Only artist search is supported ('type=artist').");

        RuleFor(v => v.Limit)
            .Must(limit => limit is null || (limit >= 1 && limit <= SpotifyOptions.SearchLimitMax))
            .WithMessage($"Limit must be between 1 and {SpotifyOptions.SearchLimitMax}.");
    }
}
