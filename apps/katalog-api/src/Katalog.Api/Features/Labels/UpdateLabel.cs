using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Katalog.Api.Features.Labels;

public enum UpdateLabelStatus
{
    Updated,
    NotFound,
    SlugConflict
}

public sealed record UpdateLabelOutcome(UpdateLabelStatus Status, LabelSummaryResponse? Response);

public sealed class UpdateLabel(KatalogContext context, TimeProvider timeProvider, ILogger<UpdateLabel> logger)
{
    public async Task<UpdateLabelOutcome> UpdateAsync(Guid labelId, string name, CancellationToken cancellationToken)
    {
        var label = await context.Labels.FirstOrDefaultAsync(l => l.Id == labelId, cancellationToken);
        if (label is null)
            return new UpdateLabelOutcome(UpdateLabelStatus.NotFound, null);

        var normalized = name.Trim();
        var slug = LabelSlug.From(normalized);
        var slugTaken = await context.Labels.AnyAsync(l => l.Slug == slug && l.Id != labelId, cancellationToken);
        if (slugTaken)
            return new UpdateLabelOutcome(UpdateLabelStatus.SlugConflict, null);

        label.Name = normalized;
        label.Slug = slug;
        label.UpdatedAtUtc = timeProvider.GetUtcNow();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsSlugUniqueViolation(ex))
        {
            logger.LogInformation("Label slug conflict rejected on update for {Slug}.", slug);
            return new UpdateLabelOutcome(UpdateLabelStatus.SlugConflict, null);
        }

        var artistCount = await context.LabelArtists.CountAsync(la => la.LabelId == labelId, cancellationToken);
        var spotifyId = await context.LabelArtists
            .Where(la => la.LabelId == labelId)
            .Select(la => la.Artist.SpotifyId)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var response = new LabelSummaryResponse(label.Id, spotifyId, label.Name, label.Slug, artistCount, label.CreatedAtUtc, label.UpdatedAtUtc);
        return new UpdateLabelOutcome(UpdateLabelStatus.Updated, response);
    }

    private static bool IsSlugUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_labels_slug"
        };
}
