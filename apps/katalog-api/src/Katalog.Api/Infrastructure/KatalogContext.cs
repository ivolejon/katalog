using Katalog.Api.Domain;
using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Infrastructure;

public sealed class KatalogContext(DbContextOptions<KatalogContext> options) : DbContext(options)
{
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<LabelArtist> LabelArtists => Set<LabelArtist>();
    public DbSet<AlbumArtist> AlbumArtists => Set<AlbumArtist>();
    public DbSet<PollCursor> PollCursors => Set<PollCursor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Postgres conventions: snake_case table/column names, no JSON columns, junction tables
        // for many-to-many, enums as int (arch report §3.4/§6.7). The snake_case convention is
        // registered on the options builder (UseSnakeCaseNamingConvention), see DatabaseSetup.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KatalogContext).Assembly);
    }
}
