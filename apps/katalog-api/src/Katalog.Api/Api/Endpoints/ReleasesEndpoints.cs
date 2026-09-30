using Katalog.Api.Contracts;
using Katalog.Api.Features.Releases;
using Microsoft.AspNetCore.Mvc;

namespace Katalog.Api.Api.Endpoints;

public static class ReleasesEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 5;
    private const int MaxPageSize = 1000;
    // Keeps page * pageSize (and the Skip offset) inside int range at MaxPageSize.
    private const int MaxPage = 1_000_000;

    public static RouteGroupBuilder MapReleasesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        // GET /api/labels/{labelId}/releases - releases for all artists under a label, paged
        // in the database. page/pageSize are optional and default to the first page at the
        // page size the UI uses.
        group.MapGet("/labels/{labelId:guid}/releases", GetLabelReleases)
            .WithName("GetLabelReleases")
            .Produces<LabelReleasesResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetLabelReleases(Guid labelId, int? page, int? pageSize,
        Guid? snapshotBoundary, GetLabelReleases getLabelReleases, CancellationToken cancellationToken)
    {
        var pageNumber = page ?? DefaultPage;
        var pageSizeValue = pageSize ?? DefaultPageSize;

        if (pageNumber < 1 || pageNumber > MaxPage || pageSizeValue < 1 || pageSizeValue > MaxPageSize)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = $"Page must be between 1 and {MaxPage}; pageSize between 1 and {MaxPageSize}.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var paged = await getLabelReleases.GetPageAsync(labelId, pageNumber, pageSizeValue, snapshotBoundary, cancellationToken);
        return paged is null ? TypedResults.NotFound() : TypedResults.Ok(paged);
    }
}
