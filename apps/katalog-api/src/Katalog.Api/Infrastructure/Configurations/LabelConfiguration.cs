using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");

        builder.HasKey(x => x.Id);

        // App-generated UUIDv7 ids; uuidv7() is also set as the DB default so raw SQL
        // inserts (albums upsert) cannot produce a missing-value error.
        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasDefaultValueSql("uuidv7()");

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamptz");

        builder.HasMany(x => x.LabelArtists)
            .WithOne(x => x.Label)
            .HasForeignKey(x => x.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
