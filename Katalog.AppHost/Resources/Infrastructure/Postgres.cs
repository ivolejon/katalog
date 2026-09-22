namespace Katalog.AppHost.Resources.Infrastructure;

public static class Postgres
{
    /// <summary>
    /// Single Postgres server with default image (postgres:18.x) and auto-generated credentials.
    /// Deliberately NO WithDataVolume early in development (arch report §5.4 fallgropar 4: avoids
    /// carrying an unmigratable local volume when the Postgres image is upgraded; AGENTS.md of the
    /// reference warns against persistent containers during early development). The database is
    /// short-lived and its schema is recreated from EF migrations at startup.
    /// </summary>
    public static IResourceBuilder<PostgresServerResource> SetupPostgres(this IDistributedApplicationBuilder builder)
    {
        return builder
            .AddPostgres("postgres")
            .WithLifetime(ContainerLifetime.Session);
    }
}
