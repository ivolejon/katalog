using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    public void Configure(EntityTypeBuilder<Album> builder)
    {
        builder.ToTable("albums");

        builder.HasKey(x => x.Id);

        // Spotify id is the natural key used for the idempotent ON CONFLICT upsert.
        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasDefaultValueSql("uuidv7()");

        builder.Property(x => x.SpotifyId)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(x => x.SpotifyId).IsUnique();

        builder.Property(x => x.Name)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.AlbumType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ReleaseDate)
            .HasColumnType("date");

        builder.Property(x => x.ReleaseDatePrecision)
            .HasConversion<int>()
            .IsRequired();

        // Raw, deprecated Spotify label field - kept for diagnostics (research §5).
        builder.Property(x => x.LabelSpotify)
            .HasMaxLength(255);

        builder.Property(x => x.LabelId);

        builder.HasIndex(x => x.LabelId);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.ExternalUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.TotalTracks)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamptz");

        builder.HasOne<Label>()
            .WithMany()
            .HasForeignKey(x => x.LabelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.AlbumArtists)
            .WithOne(x => x.Album)
            .HasForeignKey(x => x.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
