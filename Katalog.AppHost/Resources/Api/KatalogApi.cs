namespace Katalog.AppHost.Resources.Api;

public static class KatalogApi
{
    /// <summary>
    /// The single API project. WaitFor + health-check based start ordering (reference pattern):
    /// the API does not report "Running" before Postgres is up and startup migrations have run
    /// (/alive reflects the "live" health tag).
    /// </summary>
    public static IResourceBuilder<ProjectResource> SetupKatalogApi(this IDistributedApplicationBuilder builder,
        IResourceBuilder<PostgresDatabaseResource> catalogDb)
    {
        return builder
            .AddProject<Projects.Katalog_Api>("api")
            .WithReference(catalogDb)
            .WaitFor(catalogDb)
            // Fixed host port (5192) so the backend answers on the same port every run; the
            // web proxy fallback in web/vite.config.ts references this exact port.
            .WithHttpEndpoint(name: "http", port: 5192)
            .WithExternalHttpEndpoints()
            .WithHttpHealthCheck("/alive");
    }
}
