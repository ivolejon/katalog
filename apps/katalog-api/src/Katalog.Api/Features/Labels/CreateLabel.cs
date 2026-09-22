using Katalog.Api.Contracts;
using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Katalog.Api.Features.Artists;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Katalog.Api.Features.Labels;

public enum CreateLabelStatus
{
    Created,
    SlugConflict,
    ArtistNotFound
}

public sealed record CreateLabelOutcome(CreateLabelStatus Status, Label? Label, ArtistSummaryResponse? Artist);

public sealed class CreateLabel(
    KatalogContext context,
    AddArtistToLabel addArtistToLabel,
    TimeProvider timeProvider,
    ILogger<CreateLabel> logger)
{
    /// <summary>Returns the created label, or null when the slug already exists (conflict).</summary>
    public async Task<CreateLabelOutcome> CreateAsync(string name, string? spotifyId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        var slug = LabelSlug.From(normalized);

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var label = new Label
            {
                Id = Guid.CreateVersion7(),
                Name = normalized,
                Slug = slug,
                CreatedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow()
            };

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.Labels.Add(label);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsSlugUniqueViolation(ex))
            {
                logger.LogInformation("Label slug conflict rejected for {Slug}.", slug);
                return new CreateLabelOutcome(CreateLabelStatus.SlugConflict, null, null);
            }

            if (!string.IsNullOrWhiteSpace(spotifyId))
            {
                var artist = await addArtistToLabel.AddAsync(label.Id, spotifyId, cancellationToken);
                if (artist is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new CreateLabelOutcome(CreateLabelStatus.ArtistNotFound, null, null);
                }

                await transaction.CommitAsync(cancellationToken);
                return new CreateLabelOutcome(CreateLabelStatus.Created, label, artist);
            }

            await transaction.CommitAsync(cancellationToken);
            return new CreateLabelOutcome(CreateLabelStatus.Created, label, null);
        });
    }

    private static bool IsSlugUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_labels_slug"
        };
}
