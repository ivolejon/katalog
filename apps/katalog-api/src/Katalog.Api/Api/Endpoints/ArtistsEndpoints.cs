using Katalog.Api.Api.Filters;
using Katalog.Api.Api.Validators;
using Katalog.Api.Contracts;
using Katalog.Api.Features.Artists;
using Katalog.Api.Setup;

namespace Katalog.Api.Api.Endpoints;

public static class ArtistsEndpoints
{
    public static RouteGroupBuilder MapArtistsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        // GET /api/search?q=...&type=artist - artist search proxying the Spotify search endpoint.
        group.MapGet("/search", SearchArtists)
            .WithName("SearchArtists")
            .AddEndpointFilter<ValidationFilter<SearchArtistsRequest>>()
            .Produces<IReadOnlyList<ArtistSearchResult>>()
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<IResult> SearchArtists(SearchArtists searchArtists,
        [AsParameters] SearchArtistsRequest request, CancellationToken cancellationToken)
    {
        var limit = request.Limit ?? SpotifyOptions.SearchLimitDefault;
        var results = await searchArtists.SearchAsync(request.Q, limit, cancellationToken);
        return TypedResults.Ok(results);
    }
}
