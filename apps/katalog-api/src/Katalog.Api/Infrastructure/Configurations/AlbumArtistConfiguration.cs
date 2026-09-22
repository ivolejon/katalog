using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class AlbumArtistConfiguration : IEntityTypeConfiguration<AlbumArtist>
{
    public void Configure(EntityTypeBuilder<AlbumArtist> builder)
    {
        builder.ToTable("album_artists");

        builder.HasKey(x => new { x.AlbumId, x.ArtistId });

        builder.Property(x => x.Position)
            .IsRequired();
    }
}
