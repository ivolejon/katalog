using Katalog.AppHost.Resources;
using Katalog.AppHost.Resources.Api;
using Katalog.AppHost.Resources.Infrastructure;
using Aspire.Hosting.JavaScript;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure: a single Postgres server with one application database.
var postgres = builder.SetupPostgres();
var catalogDb = postgres.AddDatabase("catalog").WithResetCommand();

var api = builder.SetupKatalogApi(catalogDb);

var web = builder.AddViteApp("web", "../web", runScriptName: "dev")
    .WithReference(api)
    .WithEnvironment("API_HTTP", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(api);

await builder.Build().RunAsync();
