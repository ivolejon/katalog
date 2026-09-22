using Katalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katalog.Api.Infrastructure.Configurations;

public sealed class PollCursorConfiguration : IEntityTypeConfiguration<PollCursor>
{
    public void Configure(EntityTypeBuilder<PollCursor> builder)
    {
        builder.ToTable("poll_cursors");

        builder.HasKey(x => x.JobName);

        builder.Property(x => x.JobName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CursorValue)
            .HasColumnType("timestamptz");

        builder.Property(x => x.LastRunAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.Status)
            .HasMaxLength(50);
    }
}
