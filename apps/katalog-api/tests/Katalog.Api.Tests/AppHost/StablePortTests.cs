using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Katalog.Api.Tests.AppHost;

/// <summary>
/// The stack must answer on the same ports every run (user intent): api on 5192, web on 5173
/// and the dashboard/AppHost on https://localhost:15000. These tests pin the model-level host
/// ports (executable AppHost model) and the committed launch profile the Aspire CLI consumes
/// to pick the dashboard port.
/// </summary>
public sealed class StablePortTests
{
    [Fact]
    public async Task AppHostModel_PinsApiAndWebHostPorts()
    {
        await using var app = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Katalog_AppHost>();

        var httpPorts = app.Resources
            .Where(r => r.Name is "api" or "web")
            .Select(r => new
            {
                Name = r.Name,
                Port = r.Annotations.OfType<EndpointAnnotation>().Single(e => e.Name == "http").Port,
            })
            .ToDictionary(r => r.Name, r => r.Port);

        Assert.Equal(5192, httpPorts["api"]);
        Assert.Equal(5173, httpPorts["web"]);
    }

    [Fact]
    public void CommittedLaunchProfile_PinsDashboardPort()
    {
        // The commit:ade launch profile is the machine-consumed contract the Aspire CLI
        // uses to pick the dashboard URL; it must pin the dashboard to a fixed port.
        var launchSettingsPath = Path.Combine(AppContext.BaseDirectory, "launchSettings.json");
        using var json = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));

        var applicationUrl = json.RootElement
            .GetProperty("profiles")
            .GetProperty("https")
            .GetProperty("applicationUrl")
            .GetString();

        Assert.NotNull(applicationUrl);
        var httpsUrl = applicationUrl.Split(';')[0];
        Assert.Equal("https://localhost:15000", httpsUrl);
    }
}