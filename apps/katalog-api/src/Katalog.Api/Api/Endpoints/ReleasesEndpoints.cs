using Katalog.Api.Contracts;
using Katalog.Api.Features.Releases;

namespace Katalog.Api.Api.Endpoints;

public static class ReleasesEndpoints
{
    public static RouteGroupBuilder MapReleasesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        // GET /api/labels/{labelId}/releases - releases for all artists under a label.
        group.MapGet("/labels/{labelId:guid}/releases", GetLabelReleases)
            .WithName("GetLabelReleases")
            .Produces<IReadOnlyList<AlbumResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetLabelReleases(Guid labelId, GetLabelReleases getLabelReleases,
        CancellationToken cancellationToken)
    {
        var releases = await getLabelReleases.ListAsync(labelId, cancellationToken);
        return releases is null ? TypedResults.NotFound() : TypedResults.Ok(releases);
    }
}
