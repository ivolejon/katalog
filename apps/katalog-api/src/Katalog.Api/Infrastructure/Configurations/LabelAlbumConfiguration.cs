using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class LabelAlbumConfiguration : IEntityTypeConfiguration<LabelAlbum>
{
    public void Configure(EntityTypeBuilder<LabelAlbum> builder)
    {
        builder.ToTable("label_albums");

        builder.HasKey(x => new { x.LabelId, x.AlbumId });

        builder.Property(x => x.FirstSeenAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.LastConfirmedAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
