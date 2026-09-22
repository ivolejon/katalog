using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.ToTable("artists");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasDefaultValueSql("uuidv7()");

        // Spotify id is the natural key used for idempotent upserts.
        builder.Property(x => x.SpotifyId)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(x => x.SpotifyId).IsUnique();

        builder.Property(x => x.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.ExternalUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.Genres)
            .HasColumnType("text[]");

        builder.Property(x => x.Popularity);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamptz");

        builder.HasMany(x => x.LabelArtists)
            .WithOne(x => x.Artist)
            .HasForeignKey(x => x.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AlbumArtists)
            .WithOne(x => x.Artist)
            .HasForeignKey(x => x.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
