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
    public async Task GetLabelReleases_WithoutPagingParams_ReturnsFullList()
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

        var releases = await client.GetFromJsonAsync<IReadOnlyList<AlbumResponse>>($"/api/labels/{created!.Id}/releases");
        Assert.NotNull(releases);
        Assert.Single(releases!);
        Assert.Equal("defaultalbum1", releases![0].SpotifyId);
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

    private static async Task SeedReleasesAsync(KatalogApiFactory factory, Guid labelId,
        string artistSpotifyId, string artistName, int extraCount)
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
                ReleaseDate = new DateOnly(2020, 1, 1).AddDays(i),
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
