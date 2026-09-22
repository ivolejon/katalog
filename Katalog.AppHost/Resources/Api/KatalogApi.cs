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
            .WithHttpEndpoint(name: "http")
            .WithExternalHttpEndpoints()
            .WithHttpHealthCheck("/alive");
    }
}
