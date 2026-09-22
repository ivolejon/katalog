using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class ArtistLabelConfiguration : IEntityTypeConfiguration<LabelArtist>
{
    public void Configure(EntityTypeBuilder<LabelArtist> builder)
    {
        builder.ToTable("artist_label");

        builder.HasKey(x => new { x.LabelId, x.ArtistId });

        builder.Property(x => x.Provenance)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.FirstSeenAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.LastConfirmedAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
