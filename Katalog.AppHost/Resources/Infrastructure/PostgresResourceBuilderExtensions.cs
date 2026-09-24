using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Katalog.AppHost.Resources.Infrastructure;

public static class PostgresResourceBuilderExtensions
{
    /// <summary>
    /// Adds a "Reset Database" dashboard action that drops and recreates the database.
    /// Mirrors the reference pattern (publiceringsplattformen
    /// Host.AppHost/Resources/Infrastructure/PostgresResourceBuilderExtensions.cs):
    /// an admin connection to the "postgres" maintenance database, DROP ... WITH (FORCE),
    /// a plain CREATE, and a confirmation prompt; enabled only while the resource is healthy.
    /// </summary>
    public static IResourceBuilder<PostgresDatabaseResource> WithResetCommand(
        this IResourceBuilder<PostgresDatabaseResource> builder)
    {
        builder.WithCommand(
            name: "reset-db",
            displayName: "Reset Database",
            executeCommand: async ctx =>
            {
                var connectionExpression = builder.Resource.ConnectionStringExpression;
                var connectionString = await connectionExpression.GetValueAsync(ctx.CancellationToken);
                if (string.IsNullOrWhiteSpace(connectionString))
                    return CommandResults.Failure($"Connection string '{builder.Resource.Name}' not found.");

                try
                {
                    var csb = new NpgsqlConnectionStringBuilder(connectionString);
                    var dbName = csb.Database;
                    csb.Database = "postgres";

                    await using var admin = new NpgsqlConnection(csb.ConnectionString);
                    await admin.OpenAsync(ctx.CancellationToken);

                    var drop = $"""DROP DATABASE "{dbName}" WITH (FORCE);""";
                    var create = $"""CREATE DATABASE "{dbName}";""";

                    await using (var cmd = new NpgsqlCommand(drop, admin))
                        await cmd.ExecuteNonQueryAsync(ctx.CancellationToken);
                    await using (var cmd = new NpgsqlCommand(create, admin))
                        await cmd.ExecuteNonQueryAsync(ctx.CancellationToken);

                    return CommandResults.Success();
                }
                catch (Exception ex)
                {
                    return CommandResults.Failure($"Reset failed: {ex.Message}");
                }
            },
            commandOptions: new CommandOptions
            {
                Description = "Drop and recreate the database for a fresh start.",
                ConfirmationMessage = "Are you sure? All data will be lost.",
                IconName = "DatabaseLightning",
                IconVariant = IconVariant.Filled,
                IsHighlighted = true,
                UpdateState = c => c.ResourceSnapshot.HealthStatus == HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled
            });

        return builder;
    }
}
