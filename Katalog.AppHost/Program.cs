using Katalog.AppHost.Resources;
using Katalog.AppHost.Resources.Api;
using Katalog.AppHost.Resources.Infrastructure;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure: a single Postgres server with one application database.
var postgres = builder.SetupPostgres();
var catalogDb = postgres.AddDatabase("catalog");

// API: the only service in this task - hosts the vertical slices and the in-process
// release polling worker.
var api = builder.SetupKatalogApi(catalogDb);

// The SPA (web/) is owned by a separate frontend task. When it lands it is wired here,
// e.g.:
//   var web = builder.AddViteApp("web", "../web", runScriptName: "dev")
//       .WithReference(api)
//       .WithEnvironment("API_HTTP", api.GetEndpoint("http"))
//       .WithExternalHttpEndpoints()
//       .WaitFor(api);

await builder.Build().RunAsync();
