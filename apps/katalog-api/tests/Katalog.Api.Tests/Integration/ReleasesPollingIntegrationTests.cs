using System.Net;
using System.Net.Http.Json;
using Katalog.Api.Contracts;
using Katalog.Api.Features.Releases.Polling;
using Katalog.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Katalog.Api.Tests.Integration;

public sealed class ReleasesPollingIntegrationTests(PostgresFixture postgres, WireMockSpotify spotify)
    : IClassFixture<PostgresFixture>, IClassFixture<WireMockSpotify>
{
    private const string ArtistId = "artistpoll1xyz";
    private const string AlbumId = "albumpoll1xyz";
    private const string LabelName = "Test Label";

    /// <summary>Creates a label with one linked artist via the API, restoring a clean seed.</summary>
    private async Task<Guid> SeedLabelWithArtistAsync(KatalogApiFactory factory)
    {
        var client = factory.CreateClient();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        spotify.StubLabelSearch(LabelName,
            WireMockSpotify.AlbumItemJson(ArtistId, AlbumId, "Album 1", 2010, "Artist One"));
        // Discovery verifies each candidate's real label via GET /albums/{id}.
        spotify.StubAlbumGet(AlbumId, LabelName, ArtistId, "Album 1", 2010, "Artist One");

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);
        return label.Id;
    }

    private void StubLabelSearchWithRetry(string scenario, string albumId)
    {
        // First request in the scenario returns 429 + Retry-After; after state flips, succeeds.
        spotify.Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
                .WithParam("q", $"label:\"{LabelName}\""))
            .InScenario(scenario)
            .WillSetStateTo("succeeded")
            .RespondWith(Response.Create().WithStatusCode(429).WithHeader("Retry-After", "1"));

        spotify.Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
                .WithParam("q", $"label:\"{LabelName}\""))
            .InScenario(scenario)
            .WhenStateIs("succeeded")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.AlbumSearchJson(
                    WireMockSpotify.AlbumItemJson(ArtistId, albumId, "Album 1", 2010, "Artist One"))));

        spotify.StubAlbumGet(albumId, LabelName, ArtistId, "Album 1", 2010, "Artist One");
    }

    [Fact]
    public async Task PollOnce_UpsertsAlbumsIdempotently_AndCursorAdvances()
    {
        spotify.Reset();
        spotify.StubTokenExchange();

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var labelId = await SeedLabelWithArtistAsync(factory);

        // The label's immediate poll already discovered the album; PollOnce is idempotent.
        await RunPollerAsync(factory);
        var releases = await GetReleasesAsync(factory.CreateClient(), labelId);
        Assert.NotNull(releases);
        var album = Assert.Single(releases);
        Assert.Equal(AlbumId, album.SpotifyId);
        Assert.Equal("Album 1", album.Name);
        Assert.Equal("album", album.AlbumType);
        Assert.Equal("2010-01-15", album.ReleaseDate);
        Assert.Equal("day", album.ReleaseDatePrecision);
        Assert.Equal("Artist One", Assert.Single(album.ArtistNames));
        Assert.Equal(LabelName, album.LabelSpotify);

        // Second poll is a no-op upsert: same rows, same ids
        await RunPollerAsync(factory);
        var afterSecond = await GetReleasesAsync(factory.CreateClient(), labelId);
        Assert.NotNull(afterSecond);
        Assert.Single(afterSecond);
        Assert.Equal(releases[0].Id, afterSecond[0].Id);

        var cursor = await factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<KatalogContext>().PollCursors.FindAsync([ReleasePoller.JobName]);
        Assert.NotNull(cursor);
        Assert.Equal("completed", cursor!.Status);
    }

    [Fact]
    public async Task PollOnce_When429WithRetryAfter_RetriesAndSucceeds()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        // The custom resilience pipeline must honour Retry-After: 1 and retry after the 429.
        // Note: a fresh scenario name so the happy-path scenario from the previous test does
        // not interfere (WireMock scenarios are global per server instance).
        StubLabelSearchWithRetry($"rate-limit-{Guid.NewGuid():N}", AlbumId);

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var labelId = await SeedLabelWithArtistAsync(factory);

        await RunPollerAsync(factory);

        var releases = await GetReleasesAsync(factory.CreateClient(), labelId);
        Assert.NotNull(releases);
        Assert.Single(releases);
    }

    [Fact]
    public async Task PollOnce_WhenLabelSearchFails_SkipsLabel_ButCursorStillAdvances()
    {
        // The immediate poll during label creation must also fail, otherwise it would store the
        // successful album and the scheduled poll failure would not be observable.
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));
        spotify.Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
                .WithParam("q", $"label:\"{LabelName}\""))
            .RespondWith(Response.Create().WithStatusCode(500));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var label = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        await RunPollerAsync(factory);

        // The label search failed (500 after all retries) so no album is stored, but the cycle
        // completed and the cursor advanced.
        var releases = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(releases);
        Assert.Empty(releases);

        var cursor = await factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<KatalogContext>().PollCursors.FindAsync([ReleasePoller.JobName]);
        Assert.NotNull(cursor);
        Assert.Equal("completed", cursor!.Status);
    }

    [Fact]
    public async Task PollOnce_DiscoversOnlyLabelMatchingReleases()
    {
        // Regression: the artist discography contains two albums, but the label search only
        // returns one. PollOnce must not pull the non-matching album from the artist discography.
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        // Old artist-discography path would have returned both albums.
        spotify.StubArtistAlbums(ArtistId, "Artist One", "wrong-album", AlbumId);

        // Label search is the source of truth.
        spotify.StubLabelSearch(LabelName,
            WireMockSpotify.AlbumItemJson(ArtistId, AlbumId, "Album 1", 2010, "Artist One"));
        spotify.StubAlbumGet(AlbumId, LabelName, ArtistId, "Album 1", 2010, "Artist One");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        // Run scheduled poll: it should still only discover the label-matching album.
        await RunPollerAsync(factory);

        var releases = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(releases);
        var album = Assert.Single(releases);
        Assert.Equal(AlbumId, album.SpotifyId);
    }

    [Fact]
    public async Task PollOnce_PaginatesLabelSearchUntilExhausted()
    {
        // Regression: discovery must follow pagination so labels with more than 10 releases
        // are not silently capped.
        const string page2AlbumId = "albumpoll2xyz";

        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        // Page 1 has a full 10 items and a next pointer; page 2 has one item and no next.
        var encodedQuery = Uri.EscapeDataString($"label:\"{LabelName}\"");
        var nextUrl = $"{spotify.BaseUrl}/v1/search?q={encodedQuery}&type=album&market=SE&limit=10&offset=10";
        var page1Items = Enumerable.Range(0, 10)
            .Select(i => WireMockSpotify.AlbumItemJson(ArtistId, $"album{i}", $"Album {i}", 2010 + i, "Artist One"))
            .ToArray();
        spotify.StubLabelSearchPage(LabelName, offset: 0, next: nextUrl, page1Items);
        spotify.StubLabelSearchPage(LabelName, offset: 10, next: null,
            WireMockSpotify.AlbumItemJson(ArtistId, page2AlbumId, "Album 10", 2020, "Artist One"));

        // Every search candidate is verified via GET /albums/{id}.
        var allAlbumIds = Enumerable.Range(0, 10).Select(i => $"album{i}")
            .Append(page2AlbumId);
        foreach (var id in allAlbumIds)
        {
            spotify.StubAlbumGet(id, LabelName, ArtistId, $"Album {id}", 2010, "Artist One");
        }

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        var releases = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(releases);
        Assert.Equal(11, releases.Count);
        Assert.Contains(releases, r => r.SpotifyId == page2AlbumId);
    }

    [Fact]
    public async Task PollOnce_ExcludesAlbumWhoseRealLabelIsANearMiss()
    {
        // Regression (Globuli vs Globulin): Spotify's label search filter matches fuzzily, so
        // search on "Globuli" can return an album whose real label is "Globulin". Discovery
        // must verify each candidate via GET /albums/{id} and exclude non-exact labels.
        const string nearMissAlbumId = "albumnearmiss";

        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        spotify.StubLabelSearch(LabelName,
            WireMockSpotify.AlbumItemJson(ArtistId, nearMissAlbumId, "Near Miss Album", 2015, "Artist One"));
        spotify.StubAlbumGet(nearMissAlbumId, "Test Labels", ArtistId, "Near Miss Album", 2015, "Artist One");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        // The immediate poll on label creation must also verify, so the near-miss album
        // never lands even before the scheduled poll runs.
        var releases = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(releases);
        Assert.Empty(releases);

        await RunPollerAsync(factory);

        var afterPoll = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(afterPoll);
        Assert.Empty(afterPoll);
    }

    [Fact]
    public async Task PollOnce_IncludesExactRealLabelMatch_CaseInsensitive_AndStoresRealLabel()
    {
        // Regression (exact-match semantics): an album whose real Spotify label matches the
        // followed label exactly - case-insensitively and trimmed - must be included, and the
        // stored attribution must be the album's real label (Spotify's casing), not e.g. the
        // discovering label's name.
        const string exactAlbumId = "albumexactmatch";

        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        spotify.StubLabelSearch(LabelName,
            WireMockSpotify.AlbumItemJson(ArtistId, exactAlbumId, "Exact Album", 2016, "Artist One"));
        spotify.StubAlbumGet(exactAlbumId, $" {LabelName.ToLowerInvariant()} ", ArtistId, "Exact Album", 2016, "Artist One");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        await RunPollerAsync(factory);

        var releases = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(releases);
        var album = Assert.Single(releases);
        Assert.Equal(exactAlbumId, album.SpotifyId);
        Assert.Equal($" {LabelName.ToLowerInvariant()} ".Trim(), album.LabelSpotify);
    }

    [Fact]
    public async Task PollOnce_SkipsUnverifiableCandidate_ButCursorStillAdvances()
    {
        // A candidate the verifier cannot confirm (album GET 404 or a label-less album) is
        // skipped: discovery never upserts an unverified candidate, and the poll cycle still
        // completes so the cursor advances.
        const string unverifiableAlbumId = "albumunverifiable";

        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        spotify.StubLabelSearch(LabelName,
            WireMockSpotify.AlbumItemJson(ArtistId, unverifiableAlbumId, "Mystery Album", 2017, "Artist One"));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        await RunPollerAsync(factory);

        var releases = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(releases);
        Assert.Empty(releases);

        var cursor = await factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<KatalogContext>().PollCursors.FindAsync([ReleasePoller.JobName]);
        Assert.NotNull(cursor);
        Assert.Equal("completed", cursor!.Status);
    }

    [Fact]
    public async Task PollOnce_UnlinksPreExistingContaminatedLink_OnVerifiedMismatch_AndCorrectsLabelSpotify()
    {
        // Regression (Globuli vs Globulin, second cycle): a label_albums link created before
        // exact verification existed stays in the DB. On the next poll the discovery search
        // re-encounters the contaminated album; a positively verified real-label mismatch must
        // remove the link, while the exact-match album keeps its link and its label_spotify
        // attribution is corrected to the album's real Spotify label.
        const string exactAlbumId = "albumexacttrue";
        const string contaminatedAlbumId = "albumcontaminated";

        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{ArtistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(ArtistId, "Artist One")));

        // Seed the pre-exact-verification state: search returns both albums and both album GETs
        // report the followed label, so both are linked and attributed with the label's name.
        spotify.StubLabelSearch(LabelName,
            WireMockSpotify.AlbumItemJson(ArtistId, exactAlbumId, "True Album", 2010, "Artist One"),
            WireMockSpotify.AlbumItemJson(ArtistId, contaminatedAlbumId, "Contaminated Album", 2011, "Artist One"));
        spotify.StubAlbumGet(exactAlbumId, LabelName, ArtistId, "True Album", 2010, "Artist One");
        spotify.StubAlbumGet(contaminatedAlbumId, LabelName, ArtistId, "Contaminated Album", 2011, "Artist One");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = LabelName, spotifyIds = new[] { ArtistId } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        var seeded = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(seeded);
        Assert.Equal(2, seeded.Count);

        // Flip to the real state: the contaminated album's real label is a near miss; the true
        // album's real label differs only in case/whitespace.
        spotify.Server.Reset();
        spotify.StubTokenExchange();
        spotify.StubLabelSearch(LabelName,
            WireMockSpotify.AlbumItemJson(ArtistId, exactAlbumId, "True Album", 2010, "Artist One"),
            WireMockSpotify.AlbumItemJson(ArtistId, contaminatedAlbumId, "Contaminated Album", 2011, "Artist One"));
        spotify.StubAlbumGet(exactAlbumId, $" {LabelName.ToLowerInvariant()} ", ArtistId, "True Album", 2010, "Artist One");
        spotify.StubAlbumGet(contaminatedAlbumId, "Test Labels", ArtistId, "Contaminated Album", 2011, "Artist One");

        await RunPollerAsync(factory);

        var releases = await GetReleasesAsync(client, label.Id);
        Assert.NotNull(releases);
        var album = Assert.Single(releases);
        Assert.Equal(exactAlbumId, album.SpotifyId);
        Assert.Equal(LabelName.ToLowerInvariant(), album.LabelSpotify);
    }

    /// <summary>
    /// Fetches the label's releases through the paged endpoint, in a single page large
    /// enough for the seeded data.
    /// </summary>
    private static async Task<IReadOnlyList<AlbumResponse>> GetReleasesAsync(HttpClient client, Guid labelId)
    {
        var page = await client.GetFromJsonAsync<LabelReleasesResponse>(
            $"/api/labels/{labelId}/releases?page=1&pageSize=1000");
        return page?.Releases ?? [];
    }

    private static async Task RunPollerAsync(KatalogApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var poller = scope.ServiceProvider.GetRequiredService<ReleasePoller>();
        await poller.PollOnceAsync(CancellationToken.None);
    }
}
