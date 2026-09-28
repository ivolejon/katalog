using Katalog.Api.Contracts;
using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Katalog.Api.Features.Artists;
using Katalog.Api.Features.Releases.Polling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Katalog.Api.Features.Labels;

public enum CreateLabelStatus
{
    Created,
    SlugConflict,
    ArtistNotFound
}

public sealed record CreateLabelOutcome(CreateLabelStatus Status, Label? Label, IReadOnlyList<ArtistSummaryResponse>? Artists);

public sealed class CreateLabel(
    KatalogContext context,
    AddArtistToLabel addArtistToLabel,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<CreateLabel> logger)
{
    /// <summary>
    /// Creates a label and links the Spotify artists behind it (add-label flow: the
    /// label search returns album hits whose artists are attached, giving polling its anchors).
    /// Returns an outcome: Created with the linked artists, SlugConflict, or ArtistNotFound
    /// (any unknown Spotify id rolls the whole transaction back).
    /// </summary>
    public async Task<CreateLabelOutcome> CreateAsync(string name, IReadOnlyList<string>? spotifyIds, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        var slug = LabelSlug.From(normalized);

        var outcome = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
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

            var linkedArtists = new List<ArtistSummaryResponse>();
            foreach (var spotifyId in spotifyIds ?? [])
            {
                var artist = await addArtistToLabel.AddAsync(label.Id, spotifyId, cancellationToken);
                if (artist is null)
                {
                    logger.LogInformation("Create label {Name} rolled back: Spotify artist {SpotifyId} not found.",
                        normalized, spotifyId);
                    await transaction.RollbackAsync(cancellationToken);
                    return new CreateLabelOutcome(CreateLabelStatus.ArtistNotFound, null, null);
                }

                linkedArtists.Add(artist);
            }

            await transaction.CommitAsync(cancellationToken);

            return new CreateLabelOutcome(CreateLabelStatus.Created, label, linkedArtists);
        });

        if (outcome.Status == CreateLabelStatus.Created)
        {
            // Discover releases for the new label's artists immediately so the user does not
            // have to wait for the next scheduled poll cycle. Runs in its own scope with a
            // fresh DbContext so a polling failure cannot corrupt the request's context.
            await using var pollScope = scopeFactory.CreateAsyncScope();
            var releasePoller = pollScope.ServiceProvider.GetRequiredService<ReleasePoller>();
            await releasePoller.PollLabelAsync(outcome.Label!.Id, cancellationToken);
        }

        return outcome;
    }

    private static bool IsSlugUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_labels_slug"
        };
}
