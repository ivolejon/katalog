using Katalog.Api.Contracts;
using Katalog.Api.Features.Releases;
using Microsoft.AspNetCore.Mvc;

namespace Katalog.Api.Api.Endpoints;

public static class ReleasesEndpoints
{
    public static RouteGroupBuilder MapReleasesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        // GET /api/labels/{labelId}/releases - releases for all artists under a label.
        // Without paging query params the endpoint keeps returning the full list for older clients.
        // With page/pageSize it returns a paged response so the frontend can load incrementally.
        group.MapGet("/labels/{labelId:guid}/releases", GetLabelReleases)
            .WithName("GetLabelReleases")
            .Produces<IReadOnlyList<AlbumResponse>>()
            .Produces<LabelReleasesResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetLabelReleases(Guid labelId, int? page, int? pageSize,
        GetLabelReleases getLabelReleases, CancellationToken cancellationToken)
    {
        if (page is null && pageSize is null)
        {
            var releases = await getLabelReleases.ListAsync(labelId, cancellationToken);
            return releases is null ? TypedResults.NotFound() : TypedResults.Ok(releases);
        }

        var pageNumber = page ?? 1;
        var pageSizeValue = pageSize ?? 20;

        if (pageNumber < 1 || pageSizeValue < 1)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Page and pageSize must be positive.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var paged = await getLabelReleases.GetPageAsync(labelId, pageNumber, pageSizeValue, cancellationToken);
        return paged is null ? TypedResults.NotFound() : TypedResults.Ok(paged);
    }
}
