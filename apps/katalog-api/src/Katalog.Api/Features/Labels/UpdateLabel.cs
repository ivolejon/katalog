using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure;
using Katalog.Api.Features.Releases.Polling;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Katalog.Api.Features.Labels;

public enum UpdateLabelStatus
{
    Updated,
    NotFound,
    SlugConflict,
    RenameVerificationFailed
}

public sealed record UpdateLabelOutcome(UpdateLabelStatus Status, LabelSummaryResponse? Response);

public sealed class UpdateLabel(KatalogContext context, ReleasePoller releasePoller, TimeProvider timeProvider,
    ILogger<UpdateLabel> logger)
{
    /// <summary>
    /// Renames a label (and re-derives its slug). A rename deliberately retargets the label,
    /// so its existing junction links are audited once inside the same transaction: releases
    /// whose real Spotify label does not exactly equal the new name are unlinked, exact matches
    /// keep theirs with a corrected attribution (see
    /// <see cref="ReleasePoller.AuditLabelLinksAsync"/>). When Spotify cannot verify a link,
    /// the whole rename rolls back and the caller gets
    /// <see cref="UpdateLabelStatus.RenameVerificationFailed"/> - the label keeps its old name
    /// and all links, so nothing unverified can ever be listed.
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

        var outcome = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await context.SaveChangesAsync(cancellationToken);

                var artistCount = await context.LabelArtists.CountAsync(la => la.LabelId == labelId, cancellationToken);
                var spotifyIds = await context.LabelArtists
                    .Where(la => la.LabelId == labelId)
                    .Select(la => la.Artist.SpotifyId)
                    .ToListAsync(cancellationToken);
                var response = new LabelSummaryResponse(label.Id, spotifyIds, label.Name, label.Slug, artistCount,
                    label.CreatedAtUtc, label.UpdatedAtUtc);

                await releasePoller.AuditLabelLinksAsync(labelId, normalized, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return new UpdateLabelOutcome(UpdateLabelStatus.Updated, response);
            }
            catch (DbUpdateException ex) when (IsSlugUniqueViolation(ex))
            {
                logger.LogInformation("Label slug conflict rejected on update for {Slug}.", slug);
                return new UpdateLabelOutcome(UpdateLabelStatus.SlugConflict, null);
            }
            catch (LabelLinkAuditIncompleteException ex)
            {
                // Disposing the transaction rolls the rename and any audit writes back: the
                // label keeps its old name and all its links, so nothing unverified stays listed.
                logger.LogWarning(ex,
                    "Label rename to {Slug} rolled back: Spotify link verification failed; the label keeps its old name and links.",
                    slug);
                return new UpdateLabelOutcome(UpdateLabelStatus.RenameVerificationFailed, null);
            }
        });

        return outcome;
    }

    private static bool IsSlugUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_labels_slug"
        };
}
