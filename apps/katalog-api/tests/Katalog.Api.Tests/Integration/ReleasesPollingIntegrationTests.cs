using System.Net;
using System.Net.Http.Json;
using Katalog.Api.Contracts;
using Katalog.Api.Features.Releases.Polling;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Katalog.Api.Tests.Integration;

public sealed class ReleasesPollingIntegrationTests(PostgresFixture postgres, WireMockSpotify spotify)
    : IClassFixture<PostgresFixture>, IClassFixture<WireMockSpotify>
{
    private const string ArtistId = "artistpoll1xyz";
    private const string AlbumId = "albumpoll1xyz";

    /// <summary>Creates a label with one linked artist via the API, restoring a clean seed.</summary>
    private async Task<Guid> SeedLabelWithArtistAsync(KatalogApiFactory factory)
    {
        var client = factory.CreateClient();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        var labelResponse = await client.PostAsJsonAsync("/api/labels", new { name = "Test Label" });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        var linkResponse = await client.PostAsJsonAsync($"/api/labels/{label.Id}/artists",
            new { spotifyArtistId = ArtistId });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        return label.Id;
    }

    private void StubArtistAlbums(string scenario, string albumId)
    {
        // First request in the scenario returns 429 + Retry-After; after state flips, succeeds.
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}/albums").UsingGet())
            .InScenario(scenario)
            .WillSetStateTo("succeeded")
            .RespondWith(Response.Create().WithStatusCode(429).WithHeader("Retry-After", "1"));

        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}/albums").UsingGet())
            .InScenario(scenario)
            .WhenStateIs("succeeded")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.AlbumsJson(albumId)));
    }

    [Fact]
    public async Task PollOnce_UpsertsAlbumsIdempotently_AndCursorAdvances()
    {
        spotify.Reset();
        spotify.Reset();
        spotify.StubTokenExchange();
        StubArtistAlbums("happy-path", AlbumId);

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var labelId = await SeedLabelWithArtistAsync(factory);

        // First poll
        await RunPollerAsync(factory);
        var releases = await factory.CreateClient().GetFromJsonAsync<AlbumResponse[]>($"/api/labels/{labelId}/releases");
        Assert.NotNull(releases);
        var album = Assert.Single(releases);
        Assert.Equal(AlbumId, album.SpotifyId);
        Assert.Equal("Album 1", album.Name);
        Assert.Equal("album", album.AlbumType);
        Assert.Equal("2010-01-15", album.ReleaseDate);
        Assert.Equal("day", album.ReleaseDatePrecision);
        Assert.Equal("Artist One", Assert.Single(album.ArtistNames));

        // Second poll is a no-op upsert: same rows, same ids
        await RunPollerAsync(factory);
        var afterSecond = await factory.CreateClient().GetFromJsonAsync<AlbumResponse[]>($"/api/labels/{labelId}/releases");
        Assert.NotNull(afterSecond);
        Assert.Single(afterSecond);
        Assert.Equal(releases[0].Id, afterSecond[0].Id);
    }

    [Fact]
    public async Task PollOnce_When429WithRetryAfter_RetriesAndSucceeds()
    {
        spotify.StubTokenExchange();
        // The custom resilience pipeline must honour Retry-After: 1 and retry after the 429.
        // Note: a fresh scenario name so the happy-path scenario from the previous test does
        // not interfere (WireMock scenarios are global per server instance).
        StubArtistAlbums($"rate-limit-{Guid.NewGuid():N}", AlbumId);

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var labelId = await SeedLabelWithArtistAsync(factory);

        await RunPollerAsync(factory);

        var releases = await factory.CreateClient().GetFromJsonAsync<AlbumResponse[]>($"/api/labels/{labelId}/releases");
        Assert.NotNull(releases);
        Assert.Single(releases);
    }

    [Fact]
    public async Task PollOnce_WhenArtistCallFails_SkipsArtist_ButCursorStillAdvances()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}/albums").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var labelId = await SeedLabelWithArtistAsync(factory);

        await RunPollerAsync(factory);

        // The artist failed (500 after all retries) so no album is stored, but the cycle
        // completed and the cursor advanced (a single artist must not kill the job).
        var releases = await factory.CreateClient().GetFromJsonAsync<AlbumResponse[]>($"/api/labels/{labelId}/releases");
        Assert.NotNull(releases);
        Assert.Empty(releases);
    }

    private static async Task RunPollerAsync(KatalogApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var poller = scope.ServiceProvider.GetRequiredService<ReleasePoller>();
        await poller.PollOnceAsync(CancellationToken.None);
    }
}
