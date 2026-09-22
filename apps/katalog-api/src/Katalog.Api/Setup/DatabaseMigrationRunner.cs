using System.Data;
using EFCore.NamingConventions;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Katalog.Api.Setup;

/// <summary>
/// EF Core migration execution as a standalone CLI operation and as startup migration,
/// mirroring the reference pattern (publiceringsplattformen Shared.ServiceDefaults/
/// Database/DatabaseMigrationRunner.cs) in a simplified, single-project form.
/// </summary>
public static class DatabaseMigrationRunner
{
    /// <summary>
    /// Creates a minimal <see cref="WebApplicationBuilder"/> for migration-only runs. Content root is set to
    /// the executable directory so appsettings.*.json files are found regardless of working directory.
    /// </summary>
    public static WebApplicationBuilder CreateMigrationBuilder(string[] args)
    {
        var environmentIndex = Array.IndexOf(args, "--environment");
        var environment = environmentIndex >= 0 && environmentIndex + 1 < args.Length
            ? args[environmentIndex + 1]
            : null;

        return WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = environment
        });
    }

    /// <summary>
    /// Registers the DbContext for a migration-only run. Outside Aspire the connection string
    /// comes straight from configuration (ConnectionStrings:catalog).
    /// </summary>
    public static IServiceCollection AddMigrationDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("catalog")
            ?? throw new InvalidOperationException("Connection string 'catalog' not found.");

        services.AddDbContext<KatalogContext>(options => options
            .UseNpgsql(connectionString, postgres => postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(1), null))
            .UseSnakeCaseNamingConvention());

        return services;
    }

    /// <summary>
    /// Runs the migration or rollback operation and returns a process exit code (0 = success, 1 = failure).
    /// <c>--migrate</c> applies all pending migrations; <c>--rollback &lt;MigrationName&gt;</c> reverts to
    /// the named migration ("0" reverts all).
    /// </summary>
    public static async Task<int> RunAsync(string[] args, IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<KatalogContext>>();
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();

        try
        {
            if (args.Contains("--rollback"))
            {
                var rollbackIndex = Array.IndexOf(args, "--rollback");
                var targetMigration = rollbackIndex + 1 < args.Length ? args[rollbackIndex + 1] : null;

                if (string.IsNullOrWhiteSpace(targetMigration))
                {
                    logger.LogError("--rollback requires a target migration name. Usage: --rollback <MigrationName> ('0' reverts all)");
                    return 1;
                }

                logger.LogInformation("Rolling back database to migration: {TargetMigration}", targetMigration);
                await migrator.MigrateAsync(targetMigration);
                logger.LogInformation("Rollback completed successfully.");
                return 0;
            }

            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
            if (pendingMigrations.Count == 0)
            {
                logger.LogInformation("No pending migrations. Database is up to date.");
                return 0;
            }

            logger.LogInformation("Applying {PendingCount} pending migration(s): {Migrations}",
                pendingMigrations.Count, string.Join(", ", pendingMigrations));
            await context.Database.MigrateAsync();
            logger.LogInformation("All migrations applied successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed.");
            return 1;
        }
    }

    /// <summary>
    /// Applies pending migrations at startup for local development and testing. Skipped in
    /// deployment environments and during build-time OpenAPI generation (arch report §2.7).
    /// </summary>
    public static async Task ApplyMigrationsForNonDeploymentEnvironmentOnStartupAsync(this IHost host)
    {
        var environment = host.Services.GetRequiredService<IHostEnvironment>();
        if (OpenApiDocumentGeneration.IsActive || environment.IsStaging() || environment.IsProduction())
            return;

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<KatalogContext>>();

        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Count == 0)
            return;

        logger.LogInformation("Applying {PendingCount} pending migration(s) at startup: {Migrations}",
            pendingMigrations.Count, string.Join(", ", pendingMigrations));
        await context.Database.MigrateAsync();
    }
}
