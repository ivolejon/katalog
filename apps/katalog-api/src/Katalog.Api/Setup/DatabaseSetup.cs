using EFCore.NamingConventions;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Katalog.Api.Setup;

public static class DatabaseSetup
{
    /// <summary>
    /// Registers the EF Core context via Aspire's <c>AddNpgsqlDbContext</c> (connection name
    /// "catalog", matching <c>postgres.AddDatabase("catalog")</c> in the AppHost). This also wires
    /// the connection health check, OpenTelemetry, context pooling and retry-on-failure.
    /// During build-time OpenAPI generation the placeholder connection string from
    /// appsettings.OpenApiGeneration.json is used; the context is lazy so no Postgres is ever
    /// contacted at build time (arch report §6.3).
    /// </summary>
    public static IHostApplicationBuilder AddDatabase(this IHostApplicationBuilder builder,
        IConfiguration configuration, IHostEnvironment environment)
    {
        builder.AddNpgsqlDbContext<KatalogContext>("catalog", configureDbContextOptions: options =>
            // snake_case naming convention plugin (harmless to add after Aspire's UseNpgsql;
            // it only registers a model convention, it does not replace the connection).
            options.UseSnakeCaseNamingConvention());
        return builder;
    }
}
