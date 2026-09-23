using System.Net;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Katalog.Api.Tests.AppHost;

/// <summary>
/// Small AppHost smoke test: the distributed application model contains the expected resources,
/// the catalog database exposes the "Reset Database" dashboard action, and the API serves its
/// liveness endpoint once the stack starts (reference pattern, scaled down - arch report §3.8).
/// </summary>
public sealed class AppHostSmokeTests
{
    [Fact]
    public async Task AppHost_StartsWithExpectedResources_AndApiServesAlive()
    {
        // The API validates Spotify options on start; placeholder values keep the smoke test
        // free of real credentials and outbound calls (the poller fails gracefully on them).
        Environment.SetEnvironmentVariable("Spotify__ClientId", "placeholder");
        Environment.SetEnvironmentVariable("Spotify__ClientSecret", "placeholder");
        Environment.SetEnvironmentVariable("Spotify__BaseUrl", "https://placeholder.invalid");
        Environment.SetEnvironmentVariable("Spotify__AccountsBaseUrl", "https://placeholder.invalid");

        await using var app = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Katalog_AppHost>();

        var resourceNames = app.Resources.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("postgres", resourceNames);
        Assert.Contains("catalog", resourceNames);
        Assert.Contains("api", resourceNames);

        var catalogResource = Assert.Single(app.Resources, r => r.Name == "catalog");
        var resetCommand = catalogResource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .SingleOrDefault(c => c.Name == "reset-db");
        Assert.NotNull(resetCommand);
        Assert.Equal("Reset Database", resetCommand.DisplayName);

        var distributedApplication = await app.BuildAsync();
        await distributedApplication.StartAsync();

        var client = distributedApplication.CreateHttpClient("api");
        var alive = await client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }
}
