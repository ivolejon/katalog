using Katalog.Api.Api.Endpoints;
using Katalog.Api.Infrastructure;
using Katalog.Api.Setup;

// Migration CLI mode: `dotnet Katalog.Api.dll --migrate` / `--rollback <Migration>`.
// Used at deploy time and manually; startup migration in dev is handled below.
if (args.Contains("--migrate") || args.Contains("--rollback"))
{
    var migrateBuilder = DatabaseMigrationRunner.CreateMigrationBuilder(args);
    migrateBuilder.Services.AddMigrationDatabase(migrateBuilder.Configuration);
    var migrateApp = migrateBuilder.Build();
    using var migrateScope = migrateApp.Services.CreateScope();

    return await DatabaseMigrationRunner.RunAsync(args, migrateScope);
}

var builder = WebApplication.CreateBuilder(args);

// Placeholder settings when the process is the build-time OpenAPI generator (GetDocument.Insider).
builder.AddOpenApiDocumentGenerationSettings();

// Thin Aspire defaults: OpenTelemetry, health checks, service discovery, resilience (no global
// standard handler - Spotify needs its own, see SpotifySetup).
builder.AddKatalogServiceDefaults();

builder.Services.AddProblemDetails();

// Options with ValidateOnStart (skipped during OpenAPI generation, see OptionsSetup).
builder.Services.AddKatalogOptions(builder.Configuration, builder.Environment);

// EF Core context via Aspire (connection name "catalog").
builder.AddDatabase(builder.Configuration, builder.Environment);

// Spotify client credentials token provider + typed client with custom resilience pipeline.
builder.Services.AddSpotify(builder.Configuration, builder.Environment);

// Vertical slice features + validators + hosted release polling worker.
builder.Services.AddFeatures(builder.Environment);

// Build-time generated, committed OpenAPI document (contracts/katalog-api/openapi.json).
builder.Services.AddOpenApi();

var app = builder.Build();

await app.ApplyMigrationsForNonDeploymentEnvironmentOnStartupAsync();

app.UseExceptionHandler();

app.MapKatalogDefaultEndpoints();
app.MapOpenApi();

app.MapLabelsEndpoints();
app.MapArtistsEndpoints();
app.MapReleasesEndpoints();

app.Run();
return 0;

namespace Katalog.Api
{
    /// <summary>
    /// Marker type used by WebApplicationFactory{T} to locate the API entry assembly
    /// (the AppHost's own Program would otherwise clash with the partial Program type).
    /// </summary>
    public sealed class KatalogApiMarker { }
}
