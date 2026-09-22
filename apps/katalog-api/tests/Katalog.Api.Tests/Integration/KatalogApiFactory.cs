using Katalog.Api.Infrastructure;
using Katalog.Api.Infrastructure.Spotify;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Katalog.Api.Tests.Integration;

/// <summary>
/// WebApplicationFactory whose Postgres connection points at the Testcontainers database and
/// whose Spotify endpoints point at WireMock. Real client credentials from user secrets are
/// never used: config overrides win over user secrets in the configuration hierarchy.
/// </summary>
public sealed class KatalogApiFactory(PostgresFixture postgres, WireMockSpotify spotify)
    : WebApplicationFactory<Katalog.Api.KatalogApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:catalog", postgres.CatalogConnectionString);

        builder.UseSetting("Spotify:BaseUrl", $"{spotify.BaseUrl}/");
        builder.UseSetting("Spotify:AccountsBaseUrl", $"{spotify.BaseUrl}/");
        builder.UseSetting("Spotify:ClientId", "test-client-id");
        builder.UseSetting("Spotify:ClientSecret", "test-client-secret");
        builder.UseSetting("Spotify:Market", "SE");

        // Keep the polling worker idle in tests: a huge interval means it only does the
        // start-up poll, which with no artists is a no-op. NOTE: TimeSpan.Parse("24:00:00")
        // means 24 DAYS - use "12:00:00" (within the app's validated 6-24 h range).
        builder.UseSetting("Polling:Interval", "12:00:00");

        // The release polling hosted service must not run inside the factory: the suite drives
        // ReleasePoller deterministically itself, and a stray background poll racing the
        // test's own upserts/cursor writes is an avoidable source of flakiness (reference test
        // strategy: test environments disable background integrations).
        builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
    }

    /// <summary>Clears all data so each test starts from an empty (migrated) schema.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE TABLE album_artists, artist_label, albums, artists, labels, poll_cursors;
            """;
        await command.ExecuteNonQueryAsync();
    }
}
