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
            // The API is a dev process under `aspire run`; forcing Development makes user
            // secrets (Spotify credentials) load and keeps startup migrations enabled,
            // otherwise the default Production environment skips both and options
            // validation fails the host (arch report §2.7).
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WaitFor(catalogDb)
            // Fixed host port (5192) so the backend answers on the same port every run; the
            // web proxy fallback in web/vite.config.ts references this exact port.
            .WithHttpEndpoint(name: "http", port: 5192)
            .WithExternalHttpEndpoints()
            .WithHttpHealthCheck("/alive");
    }
}
