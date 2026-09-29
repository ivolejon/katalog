using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure;
using Katalog.Api.Features.Releases.Polling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Katalog.Api.Features.Labels;

public enum UpdateLabelStatus
{
    Updated,
    NotFound,
    SlugConflict
}

public sealed record UpdateLabelOutcome(UpdateLabelStatus Status, LabelSummaryResponse? Response);

public sealed class UpdateLabel(KatalogContext context, IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<UpdateLabel> logger)
{
    /// <summary>
    /// Renames a label (and re-derives its slug). A successful rename retargets the label, so
    /// the existing junction links are audited once afterwards: releases whose real Spotify
    /// label no longer exactly equals the new name are unlinked, exact matches keep theirs with
    /// a corrected attribution (see <see cref="ReleasePoller.AuditLabelLinksAsync"/>).
    /// </summary>
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
        var spotifyIds = await context.LabelArtists
            .Where(la => la.LabelId == labelId)
            .Select(la => la.Artist.SpotifyId)
            .ToListAsync(cancellationToken);
        var response = new LabelSummaryResponse(label.Id, spotifyIds, label.Name, label.Slug, artistCount, label.CreatedAtUtc, label.UpdatedAtUtc);

        // A rename deliberately retargets the label: without a re-encounter via search the old
        // links would never be re-verified, so audit them once right here. Runs in its own
        // scope with a fresh DbContext so a failure cannot corrupt the request's context, and
        // a failing audit cannot undo the already-committed rename.
        await using var auditScope = scopeFactory.CreateAsyncScope();
        var releasePoller = auditScope.ServiceProvider.GetRequiredService<ReleasePoller>();
        await releasePoller.AuditLabelLinksAsync(labelId, normalized, cancellationToken);

        return new UpdateLabelOutcome(UpdateLabelStatus.Updated, response);
    }

    private static bool IsSlugUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_labels_slug"
        };
}
