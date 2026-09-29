using System.Net;
using System.Net.Http.Json;
using Katalog.Api.Contracts;
using Katalog.Api.Features.Releases.Polling;
using Katalog.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Katalog.Api.Tests.Integration;

public sealed class LabelsApiIntegrationTests(PostgresFixture postgres, WireMockSpotify spotify)
    : IClassFixture<PostgresFixture>, IClassFixture<WireMockSpotify>
{
    [Fact]
    public async Task LabelsCrud_CreateListDetailUpdateDelete_WorksEndToEnd()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/artistone").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("artistone", "Fever Ray")));
        spotify.StubLabelSearch("Ninja Tune", WireMockSpotify.AlbumItemJson("artistone", "albumone", "Album One", 2010, "Fever Ray"));
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Ninja Tune", spotifyIds = new[] { "artistone" } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);
        Assert.Equal("ninja-tune", created!.Slug);

        // Releases are discovered immediately; the user does not see an empty "No releases yet" state.
        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created.Id}");
        Assert.Equal(1, detail!.ReleaseCount);
        Assert.Equal("albumone", Assert.Single(detail.Releases).SpotifyId);

        // Duplicate slug -> 409
        var duplicateResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Ninja Tune", spotifyIds = new[] { "artistone" } });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        // List
        var labels = await client.GetFromJsonAsync<LabelSummaryResponse[]>("/api/labels");
        Assert.NotNull(labels);
        var label = Assert.Single(labels);
        Assert.Equal("Ninja Tune", label.Name);

        // Detail
        detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created.Id}");
        Assert.Equal(1, detail!.ArtistCount);
        Assert.Equal("Fever Ray", Assert.Single(detail!.Artists).Name);

        // Update
        var updateResponse = await client.PutAsJsonAsync($"/api/labels/{created.Id}", new { name = "Ninja Tune Records" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(updated);
        Assert.Equal("ninja-tune-records", updated.Slug);

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/labels/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/labels/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task CreateLabel_InvalidName_ReturnsValidationProblem()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/labels", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLabel_RequiresUniqueArtistIds()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var emptyResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "No Artists", spotifyIds = Array.Empty<string>() });
        var duplicateResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Duplicate Artists", spotifyIds = new[] { "artistone", "artistone" } });

        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task AddArtistToLabel_LinksArtist_AndDetailShowsIt()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/artistone").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("artistone", "Fever Ray")));
        spotify.StubLabelSearch("Rabid Records", WireMockSpotify.AlbumItemJson("artistone", "albumone", "Album One", 2010, "Fever Ray"));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Rabid Records", spotifyIds = new[] { "artistone" } });
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(label);

        var linkResponse = await client.PostAsJsonAsync($"/api/labels/{label.Id}/artists",
            new { spotifyArtistId = "artistone" });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        var linked = await linkResponse.Content.ReadFromJsonAsync<ArtistSummaryResponse>();
        Assert.NotNull(linked);
        Assert.Equal("Fever Ray", linked.Name);
        Assert.Equal("artistone", linked.SpotifyId);

        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{label.Id}");
        Assert.Equal(1, detail!.ArtistCount);
        Assert.Equal("Fever Ray", Assert.Single(detail!.Artists).Name);

        // Linking the same artist again stays idempotent (single artist row)
        var secondLink = await client.PostAsJsonAsync($"/api/labels/{label.Id}/artists",
            new { spotifyArtistId = "artistone" });
        Assert.Equal(HttpStatusCode.OK, secondLink.StatusCode);

        var detailAfterRepeat = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{label.Id}");
        Assert.Equal(1, detailAfterRepeat!.ArtistCount);

        // Unknown artist on Spotify -> 404
        var missing = await client.PostAsJsonAsync($"/api/labels/{label.Id}/artists",
            new { spotifyArtistId = "doesnotexist123" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // A second anchor allows removing one artist while the label keeps a follow
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/artisttwo").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("artisttwo", "Olof Dreijer")));
        var secondAnchor = await client.PostAsJsonAsync($"/api/labels/{label.Id}/artists",
            new { spotifyArtistId = "artisttwo" });
        Assert.Equal(HttpStatusCode.OK, secondAnchor.StatusCode);

        var removeResponse = await client.DeleteAsync($"/api/labels/{label.Id}/artists/{linked.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        var detailAfterRemove = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{label.Id}");
        Assert.Equal(1, detailAfterRemove!.ArtistCount);

        // Removing the last artist is refused: a followed label keeps at least one anchor
        var remaining = Assert.Single(detailAfterRemove.Artists);
        var lastRemove = await client.DeleteAsync($"/api/labels/{label.Id}/artists/{remaining.Id}");
        Assert.Equal(HttpStatusCode.Conflict, lastRemove.StatusCode);
        var detailAfterRefused = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{label.Id}");
        Assert.Equal(1, detailAfterRefused!.ArtistCount);
        Assert.Equal("Olof Dreijer", Assert.Single(detailAfterRefused.Artists).Name);
    }

    [Fact]
    public async Task SearchArtists_ProxiesSpotifySearch()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        const string searchJson = """
            {"artists": {"items": [{"id":"artistsearch1","name":"Karin Dreijer","images":[],"external_urls":{"spotify":"https://open.spotify.com/artist/artist-search-1"},"genres":["electronic"],"popularity":60}],"total":1}}
            """;
        spotify.Server.Given(Request.Create().WithPath("/v1/search").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(searchJson));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search?q=karin+dreijer&type=artist");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<ArtistSearchResult[]>();
        Assert.NotNull(results);
        var result = Assert.Single(results);
        Assert.Equal("artistsearch1", result.Id);
        Assert.Equal("Karin Dreijer", result.Name);
    }

    [Fact]
    public async Task SearchArtists_WithLimitAboveMax_RejectsWith400()
    {
        // Spotify caps search limit at 10 (spec, verified 2026-09-22); the Katalog API rejects
        // a larger limit with 400 before forwarding to Spotify.
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search?q=karin+dreijer&type=artist&limit=50");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchLabels_ProxiesSpotifyAlbumSearch_WithLabelFilter()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        // The raw wire query is q=label%3A%22Globuli%22... ; WireMock matches the decoded value,
        // so asserting "label:\"Globuli\"" proves the quotes survived URI encoding and the
        // label: filter reaches Spotify unchanged (verified live 2026-09-22).
        var albums = new[]
        {
            WireMockSpotify.AlbumItemJson("labelartist1", "labelalbum1", "Daydream Forever", 2023),
            WireMockSpotify.AlbumItemJson("labelartist2", "labelalbum2", "Era", 2021),
        };
        var searchJson = WireMockSpotify.AlbumSearchJson(albums);
        spotify.Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
            .WithParam("q", "label:\"Globuli\"")
            .WithParam("type", "album")
            .WithParam("market", "SE")
            .WithParam("limit", "10"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(searchJson));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/labels/search?q=Globuli");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LabelSearchResponse>();
        Assert.NotNull(result);
        Assert.Equal("Globuli", result!.Query);
        var label = Assert.Single(result.Labels);
        Assert.Equal("Globuli", label.Name);
        Assert.Equal(2, label.Albums.Count);
        var first = label.Albums[0];
        Assert.Equal("labelalbum1", first.AlbumId);
        Assert.Equal("Daydream Forever", first.Name);
        Assert.Equal("2023-01-15", first.ReleaseDate);
        Assert.Equal("labelartist1", Assert.Single(first.Artists).SpotifyId);
        Assert.Equal("https://open.spotify.com/album/labelalbum1", first.ExternalUrl);
    }

    [Fact]
    public async Task SearchLabels_EscapesQuotesInsideLabelFilter()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
                .WithParam("q", "label:\"ACME \\\"Records\\\"\"")
                .WithParam("type", "album")
                .WithParam("market", "SE")
                .WithParam("limit", "10"))
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.AlbumSearchJson([])));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var response = await factory.CreateClient().GetAsync("/api/labels/search?q=ACME%20%22Records%22");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SearchLabels_WithLimitAboveMax_RejectsWith400()
    {
        // Spotify caps search limit at 10 (limits PR, verified live 2026-09-22); the Katalog API
        // rejects a larger limit with 400 before forwarding to Spotify.
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/labels/search?q=Globuli&limit=11");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var zeroLimit = await client.GetAsync("/api/labels/search?q=Globuli&limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, zeroLimit.StatusCode);

        var blankQuery = await client.GetAsync("/api/labels/search?q=%20%20");
        Assert.Equal(HttpStatusCode.BadRequest, blankQuery.StatusCode);
    }

    [Fact]
    public async Task CreateLabel_WithMultipleSpotifyIds_LinksAllArtists()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/artistone").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("artistone", "Karin Dreijer")));
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/artisttwo").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("artisttwo", "Fever Ray")));
        spotify.StubLabelSearch("Rabid Records",
            WireMockSpotify.AlbumItemJson("artistone", "albumone", "Album One", 2010, "Karin Dreijer"),
            WireMockSpotify.AlbumItemJson("artisttwo", "albumtwo", "Album Two", 2011, "Fever Ray"));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Rabid Records", spotifyIds = new[] { "artistone", "artisttwo" } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);
        Assert.Equal(2, created!.ArtistCount);
        Assert.Equal(new[] { "artistone", "artisttwo" }, created.SpotifyIds);

        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created.Id}");
        Assert.Equal(2, detail!.ArtistCount);
        Assert.Equal(new[] { "Fever Ray", "Karin Dreijer" },
            detail.Artists.Select(a => a.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task CreateLabel_DiscoversReleasesImmediately()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/immediate1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("immediate1", "Immediate Artist")));
        spotify.StubLabelSearch("Immediate Label",
            WireMockSpotify.AlbumItemJson("immediate1", "immediatealbum1", "Immediate Album 1", 2010, "Immediate Artist"),
            WireMockSpotify.AlbumItemJson("immediate1", "immediatealbum2", "Immediate Album 2", 2011, "Immediate Artist"));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Immediate Label", spotifyIds = new[] { "immediate1" } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        // Releases are discovered synchronously during create, before any scheduled poll runs.
        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created.Id}");
        Assert.Equal(2, detail!.ReleaseCount);
        Assert.Equal(new[] { "immediatealbum1", "immediatealbum2" },
            detail.Releases.Select(r => r.SpotifyId).OrderBy(id => id).ToArray());

        // Polling cursor is unchanged: the immediate poll does not advance the scheduled job cursor.
        var cursor = await factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<KatalogContext>().PollCursors.FindAsync([ReleasePoller.JobName]);
        Assert.Null(cursor);
    }

    [Fact]
    public async Task CreateLabel_WhenAlbumPollingFails_StillCreatesLabel()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/pollfail1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("pollfail1", "Poll Fail Artist")));
        // Stub the label search with a 500 so the immediate poll fails but the label is still created.
        spotify.Server.Given(Request.Create().WithPath("/v1/search").UsingGet()
                .WithParam("q", "label:\"Poll Fail Label\""))
            .RespondWith(Response.Create().WithStatusCode(500));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Poll Fail Label", spotifyIds = new[] { "pollfail1" } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        // The label exists even though the immediate release poll failed.
        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created.Id}");
        Assert.Equal("Poll Fail Label", detail!.Name);
        Assert.Equal(0, detail.ReleaseCount);
    }

    [Fact]
    public async Task CreateLabel_WithUnknownSpotifyId_RollsBackWith404()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/artistone").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("artistone", "Karin Dreijer")));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        // Second id is unknown to Spotify -> whole create rolls back, nothing is persisted.
        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Rabid Records", spotifyIds = new[] { "artistone", "doesnotexist123" } });
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);

        var labels = await client.GetFromJsonAsync<LabelSummaryResponse[]>("/api/labels");
        Assert.Empty(labels!);
    }

    [Fact]
    public async Task CreateLabel_DiscoversOnlyLabelMatchingReleases()
    {
        // Regression: the artist's discography contains two albums, but only one is returned by
        // the label-filtered search. The release feed must exclude the non-matching album.
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/globartist1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("globartist1", "Globuli Artist")));

        // The old buggy path would have pulled both albums from the artist discography.
        spotify.StubArtistAlbums("globartist1", "Globuli Artist", "globuli-correct", "globuli-wrong");

        // The label search is the source of truth: only the album that actually belongs to the label.
        spotify.StubLabelSearch("Globuli",
            WireMockSpotify.AlbumItemJson("globartist1", "globuli-correct", "Correct Album", 2020, "Globuli Artist"));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Globuli", spotifyIds = new[] { "globartist1" } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created.Id}");
        Assert.Equal(1, detail!.ReleaseCount);
        var release = Assert.Single(detail.Releases);
        Assert.Equal("globuli-correct", release.SpotifyId);
    }

    [Fact]
    public async Task CreateLabel_AlbumSharedByTwoLabels_AppearsUnderBoth()
    {
        // Regression: an album returned by label searches for two different followed labels
        // must appear under both labels. The label_albums junction table stores each link
        // independently, so discovering the album for label B does not remove it from label A.
        const string sharedAlbumId = "sharedalbum1";
        const string artistId = "sharedartist1";

        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath($"/v1/artists/{artistId}").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson(artistId, "Shared Artist")));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        // Label A search returns the shared album.
        spotify.StubLabelSearch("Label A",
            WireMockSpotify.AlbumItemJson(artistId, sharedAlbumId, "Shared Album", 2020, "Shared Artist"));
        var createA = await client.PostAsJsonAsync("/api/labels",
            new { name = "Label A", spotifyIds = new[] { artistId } });
        Assert.Equal(HttpStatusCode.Created, createA.StatusCode);
        var labelA = await createA.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(labelA);

        // Label B search also returns the same shared album.
        spotify.StubLabelSearch("Label B",
            WireMockSpotify.AlbumItemJson(artistId, sharedAlbumId, "Shared Album", 2020, "Shared Artist"));
        var createB = await client.PostAsJsonAsync("/api/labels",
            new { name = "Label B", spotifyIds = new[] { artistId } });
        Assert.Equal(HttpStatusCode.Created, createB.StatusCode);
        var labelB = await createB.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(labelB);

        var detailA = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{labelA.Id}");
        var detailB = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{labelB.Id}");

        Assert.Equal(1, detailA!.ReleaseCount);
        Assert.Equal(sharedAlbumId, Assert.Single(detailA.Releases).SpotifyId);

        Assert.Equal(1, detailB!.ReleaseCount);
        Assert.Equal(sharedAlbumId, Assert.Single(detailB.Releases).SpotifyId);
    }
}
