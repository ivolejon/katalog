using Katalog.AppHost.Resources;
using Katalog.AppHost.Resources.Api;
using Katalog.AppHost.Resources.Infrastructure;
using Aspire.Hosting.JavaScript;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure: a single Postgres server with one application database.
var postgres = builder.SetupPostgres();
var catalogDb = postgres.AddDatabase("catalog");

var api = builder.SetupKatalogApi(catalogDb);

// All three stable-port owners (same URLs every `aspire run`):
//  - dashboard/AppHost: Katalog.AppHost/Properties/launchSettings.json `applicationUrl`
//    (https://localhost:15000; the launch profile is the mechanism the Aspire CLI 13.5.x
//    uses to pin the dashboard port - without it the CLI assigns a random port per run).
//  - api: fixed host port 5192 in Resources/Api/KatalogApi.cs (WithHttpEndpoint port).
//  - web: fixed host port 5173 below (WithHttpEndpoint port) - must match Vite's own
//    default port so `vite --port 5173` serves the frontend where docs say it lives.
var web = builder.AddViteApp("web", "../web", runScriptName: "dev")
    .WithHttpEndpoint(name: "http", port: 5173)
    .WithReference(api)
    .WithEnvironment("API_HTTP", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(api);

await builder.Build().RunAsync();
