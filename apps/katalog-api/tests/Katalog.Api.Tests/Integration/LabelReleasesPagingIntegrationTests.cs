using System.Net;
using System.Net.Http.Json;
using Katalog.Api.Contracts;
using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Katalog.Api.Tests.Integration;

public sealed class LabelReleasesPagingIntegrationTests(PostgresFixture postgres, WireMockSpotify spotify, ITestOutputHelper output)
    : IClassFixture<PostgresFixture>, IClassFixture<WireMockSpotify>
{
    [Fact]
    public async Task GetLabelReleases_PagesThroughReleases_UntilExhausted()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/pageartist1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("pageartist1", "Paging Artist")));
        spotify.StubLabelSearch("Paging Label",
            WireMockSpotify.AlbumItemJson("pageartist1", "pagealbum1", "Page Album 1", 2024, "Paging Artist"));
        spotify.StubAlbumGet("pagealbum1", "Paging Label", "pageartist1", "Page Album 1", 2024, "Paging Artist");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Paging Label", spotifyIds = new[] { "pageartist1" } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        // Seed 9 additional releases so we have 10 total and can exercise multiple pages.
        await SeedReleasesAsync(factory, created!.Id, "pageartist1", "Paging Artist", 9);

        // Page 1: three releases, more available.
        var page1 = await client.GetFromJsonAsync<LabelReleasesResponse>($"/api/labels/{created.Id}/releases?page=1&pageSize=3");
        Assert.NotNull(page1);
        Assert.Equal(1, page1!.Page);
        Assert.Equal(3, page1.PageSize);
        Assert.Equal(10, page1.TotalCount);
        Assert.Equal(3, page1.Releases.Count);
        Assert.True(page1.HasMore);

        // Page 4: the last page with a single release.
        var page4 = await client.GetFromJsonAsync<LabelReleasesResponse>($"/api/labels/{created.Id}/releases?page=4&pageSize=3");
        Assert.NotNull(page4);
        Assert.Equal(4, page4!.Page);
        Assert.Single(page4.Releases);
        Assert.False(page4.HasMore);

        // Page 5: beyond the end, empty and no more pages.
        var page5 = await client.GetFromJsonAsync<LabelReleasesResponse>($"/api/labels/{created.Id}/releases?page=5&pageSize=3");
        Assert.NotNull(page5);
        Assert.Equal(5, page5!.Page);
        Assert.Empty(page5.Releases);
        Assert.False(page5.HasMore);
    }

    [Fact]
    public async Task GetLabelReleases_WithoutPagingParams_ReturnsDefaultFirstPage()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/defaultartist1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("defaultartist1", "Default Artist")));
        spotify.StubLabelSearch("Default Label",
            WireMockSpotify.AlbumItemJson("defaultartist1", "defaultalbum1", "Default Album 1", 2024, "Default Artist"));
        spotify.StubAlbumGet("defaultalbum1", "Default Label", "defaultartist1", "Default Album 1", 2024, "Default Artist");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Default Label", spotifyIds = new[] { "defaultartist1" } });
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        var page = await client.GetFromJsonAsync<LabelReleasesResponse>($"/api/labels/{created!.Id}/releases");
        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(1, page.TotalCount);
        Assert.False(page.HasMore);
        var release = Assert.Single(page.Releases);
        Assert.Equal("defaultalbum1", release.SpotifyId);
    }

    [Fact]
    public async Task GetLabelReleases_TiedReleaseDates_ReturnsEveryReleaseExactlyOnce()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/tiedartist1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("tiedartist1", "Tied Artist")));
        spotify.StubLabelSearch("Tied Label",
            WireMockSpotify.AlbumItemJson("tiedartist1", "tiedalbum1", "Tied Album 1", 2024, "Tied Artist"));
        spotify.StubAlbumGet("tiedalbum1", "Tied Label", "tiedartist1", "Tied Album 1", 2024, "Tied Artist");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Tied Label", spotifyIds = new[] { "tiedartist1" } });
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        // Six more releases, all sharing one release date: ORDER BY alone cannot
        // distinguish them, so paging must rely on the id tiebreaker to avoid
        // skipping or duplicating rows across pages.
        await SeedReleasesAsync(factory, created!.Id, "tiedartist1", "Tied Artist", 6,
            new DateOnly(2024, 6, 1));

        var loaded = new List<Guid>();
        var pageNumber = 1;
        var hasMore = true;
        while (hasMore)
        {
            var page = await client.GetFromJsonAsync<LabelReleasesResponse>($"/api/labels/{created.Id}/releases?page={pageNumber}&pageSize=3");
            Assert.NotNull(page);
            Assert.Equal(7, page!.TotalCount);
            Assert.Equal(pageNumber, page.Page);
            Assert.Equal(3, page.PageSize);
            loaded.AddRange(page.Releases.Select(r => r.Id));
            hasMore = page.HasMore;
            pageNumber++;
        }

        Assert.Equal(7, loaded.Distinct().Count());
        Assert.Equal(7, loaded.Count);
    }

    [Fact]
    public async Task GetLabelReleases_InvalidPageOrPageSize_Returns400()
    {
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var zeroPage = await client.GetAsync("/api/labels/00000000-0000-0000-0000-000000000000/releases?page=0&pageSize=3");
        var zeroPageBody = await zeroPage.Content.ReadAsStringAsync();
        output.WriteLine($"ZERO PAGE STATUS: {zeroPage.StatusCode}");
        output.WriteLine($"ZERO PAGE BODY: {zeroPageBody}");
        var zeroSize = await client.GetAsync("/api/labels/00000000-0000-0000-0000-000000000000/releases?page=1&pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, zeroPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zeroSize.StatusCode);
    }

    [Fact]
    public async Task GetLabelDetail_WithoutReleases_KeepsShapeAndCounters()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/shapeartist1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("shapeartist1", "Shape Artist")));
        spotify.StubLabelSearch("Shape Label",
            WireMockSpotify.AlbumItemJson("shapeartist1", "shapealbum1", "Shape Album 1", 2024, "Shape Artist"));
        spotify.StubAlbumGet("shapealbum1", "Shape Label", "shapeartist1", "Shape Album 1", 2024, "Shape Artist");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Shape Label", spotifyIds = new[] { "shapeartist1" } });
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created!.Id}?includeReleases=false");
        Assert.NotNull(detail);
        Assert.Equal("Shape Label", detail!.Name);
        Assert.Equal(1, detail.ArtistCount);
        Assert.Equal(1, detail.ReleaseCount);
        Assert.Empty(detail.Releases);
        Assert.Single(detail.Artists);
    }

    [Fact]
    public async Task GetLabelDetail_WithReleasesDefault_IncludesReleases()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/backcompatartist1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("backcompatartist1", "Backcompat Artist")));
        spotify.StubLabelSearch("Backcompat Label",
            WireMockSpotify.AlbumItemJson("backcompatartist1", "backcompatalbum1", "Backcompat Album 1", 2024, "Backcompat Artist"));
        spotify.StubAlbumGet("backcompatalbum1", "Backcompat Label", "backcompatartist1", "Backcompat Album 1", 2024, "Backcompat Artist");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Backcompat Label", spotifyIds = new[] { "backcompatartist1" } });
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created!.Id}");
        Assert.NotNull(detail);
        Assert.Single(detail!.Releases);
        Assert.Equal("backcompatalbum1", detail.Releases[0].SpotifyId);
    }

    [Fact]
    public async Task GetLabelReleases_SnapshotBoundary_PreventsMidPagingInserts()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/snapshotartist1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("snapshotartist1", "Snapshot Artist")));
        spotify.StubLabelSearch("Snapshot Label",
            WireMockSpotify.AlbumItemJson("snapshotartist1", "snapshotalbum1", "Snapshot Album 1", 2024, "Snapshot Artist"));
        spotify.StubAlbumGet("snapshotalbum1", "Snapshot Label", "snapshotartist1", "Snapshot Album 1", 2024, "Snapshot Artist");

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/labels",
            new { name = "Snapshot Label", spotifyIds = new[] { "snapshotartist1" } });
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);

        // Seed 4 additional releases (5 total with the discovered one).
        await SeedReleasesAsync(factory, created!.Id, "snapshotartist1", "Snapshot Artist", 4);

        // Page 1: 3 releases, with snapshot boundary.
        var page1 = await client.GetFromJsonAsync<LabelReleasesResponse>(
            $"/api/labels/{created.Id}/releases?page=1&pageSize=3");
        Assert.NotNull(page1);
        Assert.Equal(3, page1!.Releases.Count);
        Assert.NotNull(page1.SnapshotBoundary);
        Assert.True(page1.HasMore);

        // Insert a newer album between page 1 and page 2 (simulating background discovery).
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
            var artist = await context.Artists.SingleAsync(a => a.SpotifyId == "snapshotartist1");
            var label = await context.Labels.SingleAsync(l => l.Id == created.Id);
            var now = DateTimeOffset.UtcNow;
            var newAlbumId = Guid.CreateVersion7();
            var newAlbum = new Album
            {
                Id = newAlbumId,
                SpotifyId = "newalbum1",
                Name = "New Album",
                AlbumType = AlbumType.Album,
                ReleaseDate = new DateOnly(2025, 6, 1),
                ReleaseDatePrecision = ReleaseDatePrecision.Day,
                LabelSpotify = label.Name,
                LabelId = label.Id,
                ExternalUrl = "https://open.spotify.com/album/newalbum1",
                TotalTracks = 10,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            context.Albums.Add(newAlbum);
            context.LabelAlbums.Add(new LabelAlbum
            {
                LabelId = label.Id,
                AlbumId = newAlbum.Id,
                FirstSeenAtUtc = now,
                LastConfirmedAtUtc = now,
            });
            context.AlbumArtists.Add(new AlbumArtist
            {
                AlbumId = newAlbum.Id,
                ArtistId = artist.Id,
                Position = 0,
            });
            await context.SaveChangesAsync();
        }

        // Page 2: with snapshot boundary, the new album must not appear.
        var page2 = await client.GetFromJsonAsync<LabelReleasesResponse>(
            $"/api/labels/{created.Id}/releases?page=2&pageSize=3&snapshotBoundary={page1.SnapshotBoundary}");
        Assert.NotNull(page2);
        Assert.Equal(2, page2!.Releases.Count);
        Assert.False(page2.HasMore);

        // The union of pages 1 and 2 equals exactly the original 5 releases.
        var allLoaded = page1.Releases.Concat(page2.Releases).ToList();
        Assert.Equal(5, allLoaded.Count);
        Assert.Equal(5, allLoaded.Select(r => r.Id).Distinct().Count());

        // The new album is not listed in this session.
        Assert.DoesNotContain(allLoaded, r => r.Name == "New Album");
    }

    private static async Task SeedReleasesAsync(KatalogApiFactory factory, Guid labelId,
        string artistSpotifyId, string artistName, int extraCount, DateOnly? fixedReleaseDate = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KatalogContext>();
        var artist = await context.Artists.SingleAsync(a => a.SpotifyId == artistSpotifyId);
        var label = await context.Labels.SingleAsync(l => l.Id == labelId);
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < extraCount; i++)
        {
            var albumId = Guid.CreateVersion7();
            var album = new Album
            {
                Id = albumId,
                SpotifyId = $"seedalbum{i}",
                Name = $"Seed Album {i}",
                AlbumType = AlbumType.Album,
                ReleaseDate = fixedReleaseDate ?? new DateOnly(2020, 1, 1).AddDays(i),
                ReleaseDatePrecision = ReleaseDatePrecision.Day,
                LabelSpotify = label.Name,
                LabelId = label.Id,
                ExternalUrl = $"https://open.spotify.com/album/seedalbum{i}",
                TotalTracks = 10,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            context.Albums.Add(album);
            context.LabelAlbums.Add(new LabelAlbum
            {
                LabelId = label.Id,
                AlbumId = album.Id,
                FirstSeenAtUtc = now,
                LastConfirmedAtUtc = now,
            });
            context.AlbumArtists.Add(new AlbumArtist
            {
                AlbumId = album.Id,
                ArtistId = artist.Id,
                Position = 0,
            });
        }

        await context.SaveChangesAsync();
    }
}
