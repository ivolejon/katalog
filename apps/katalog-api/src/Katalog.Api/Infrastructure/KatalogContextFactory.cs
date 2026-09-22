using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Katalog.Api.Infrastructure;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to build a <see cref="KatalogContext"/>
/// without booting the full app (which would need a live Postgres connection string from
/// Aspire). The runtime registration uses Aspire's <c>AddNpgsqlDbContext</c> instead.
/// </summary>
public sealed class KatalogContextFactory : IDesignTimeDbContextFactory<KatalogContext>
{
    public KatalogContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<KatalogContext>();
        var connectionString = Environment.GetEnvironmentVariable("KATALOG_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=catalog;Include Error Detail=true";

        builder
            .UseNpgsql(connectionString, postgres => postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(1), null))
            .UseSnakeCaseNamingConvention();

        return new KatalogContext(builder.Options);
    }
}
