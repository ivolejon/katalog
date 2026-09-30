using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class SpotifyUserSessionConfiguration : IEntityTypeConfiguration<SpotifyUserSession>
{
    public void Configure(EntityTypeBuilder<SpotifyUserSession> builder)
    {
        builder.ToTable("spotify_user_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever()
            .HasDefaultValueSql("uuidv7()");

        builder.Property(x => x.SpotifyUserId)
            .HasMaxLength(64)
            .IsRequired();

        // One row per Spotify account: a second sign-in replaces the existing row.
        builder.HasIndex(x => x.SpotifyUserId).IsUnique();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(255);

        builder.Property(x => x.Product)
            .HasMaxLength(50);

        // Tokens are plain columns: the project stores no JSON, and nothing in the app logs a
        // token value (EF sensitive-data logging is not enabled anywhere in Katalog).
        builder.Property(x => x.AccessToken).IsRequired();
        builder.Property(x => x.RefreshToken).IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.Scope)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamptz");
    }
}
