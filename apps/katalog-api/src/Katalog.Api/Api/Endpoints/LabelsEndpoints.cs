using Katalog.Api.Api.Filters;
using Katalog.Api.Api.Validators;
using Katalog.Api.Contracts;
using Katalog.Api.Features.Artists;
using Katalog.Api.Features.Labels;
using Microsoft.AspNetCore.Mvc;

namespace Katalog.Api.Api.Endpoints;

public static class LabelsEndpoints
{
    public static RouteGroupBuilder MapLabelsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/labels");

        group.MapGet("/", ListLabels)
            .WithName("ListLabels")
            .Produces<IReadOnlyList<LabelSummaryResponse>>();

        group.MapPost("/", CreateLabel)
            .WithName("CreateLabel")
            .AddEndpointFilter<ValidationFilter<CreateLabelRequest>>()
            .Produces<LabelSummaryResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetLabelDetail)
            .WithName("GetLabelDetail")
            .Produces<LabelDetailResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateLabel)
            .WithName("UpdateLabel")
            .AddEndpointFilter<ValidationFilter<UpdateLabelRequest>>()
            .Produces<LabelSummaryResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteLabel)
            .WithName("DeleteLabel")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{labelId:guid}/artists", AddArtistToLabel)
            .WithName("AddArtistToLabel")
            .AddEndpointFilter<ValidationFilter<AddArtistToLabelRequest>>()
            .Produces<ArtistSummaryResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{labelId:guid}/artists/{artistId:guid}", RemoveArtistFromLabel)
            .WithName("RemoveArtistFromLabel")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> ListLabels(GetLabels getLabels, CancellationToken cancellationToken)
        => TypedResults.Ok(await getLabels.ListAsync(cancellationToken));

    private static async Task<IResult> CreateLabel(CreateLabel createLabel, CreateLabelRequest request,
        CancellationToken cancellationToken)
    {
        var label = await createLabel.CreateAsync(request.Name, cancellationToken);
        if (label is null)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "A label with this name already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return TypedResults.Created($"/api/labels/{label.Id}",
            new LabelSummaryResponse(label.Id, label.Name, label.Slug, 0, label.CreatedAtUtc, label.UpdatedAtUtc));
    }

    private static async Task<IResult> GetLabelDetail(Guid id, GetLabels getLabels, CancellationToken cancellationToken)
    {
        var detail = await getLabels.GetDetailAsync(id, cancellationToken);
        return detail is null ? TypedResults.NotFound() : TypedResults.Ok(detail);
    }

    private static async Task<IResult> UpdateLabel(Guid id, UpdateLabel updateLabel, UpdateLabelRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await updateLabel.UpdateAsync(id, request.Name, cancellationToken);
        return outcome.Status switch
        {
            UpdateLabelStatus.Updated => TypedResults.Ok(outcome.Response),
            UpdateLabelStatus.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Conflict(new ProblemDetails
            {
                Title = "A label with this name already exists.",
                Status = StatusCodes.Status409Conflict
            })
        };
    }

    private static async Task<IResult> DeleteLabel(Guid id, DeleteLabel deleteLabel, CancellationToken cancellationToken)
        => await deleteLabel.DeleteAsync(id, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();

    private static async Task<IResult> AddArtistToLabel(Guid labelId, AddArtistToLabel addArtistToLabel,
        AddArtistToLabelRequest request, CancellationToken cancellationToken)
    {
        var artist = await addArtistToLabel.AddAsync(labelId, request.SpotifyArtistId, cancellationToken);
        return artist is null ? TypedResults.NotFound() : TypedResults.Ok(artist);
    }

    private static async Task<IResult> RemoveArtistFromLabel(Guid labelId, Guid artistId,
        RemoveArtistFromLabel removeArtistFromLabel, CancellationToken cancellationToken)
        => await removeArtistFromLabel.RemoveAsync(labelId, artistId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
