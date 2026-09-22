using Katalog.Api.Contracts;
using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Katalog.Api.Features.Labels;

public sealed class CreateLabel(KatalogContext context, TimeProvider timeProvider, ILogger<CreateLabel> logger)
{
    /// <summary>Returns the created label, or null when the slug already exists (conflict).</summary>
    public async Task<Label?> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = name.Trim();
        var slug = LabelSlug.From(normalized);

        var label = new Label
        {
            Id = Guid.CreateVersion7(),
            Name = normalized,
            Slug = slug,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };

        context.Labels.Add(label);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsSlugUniqueViolation(ex))
        {
            logger.LogInformation("Label slug conflict rejected for {Slug}.", slug);
            return null;
        }

        return label;
    }

    private static bool IsSlugUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_labels_slug"
        };
}
