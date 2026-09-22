using System.Net;
using System.Net.Http.Json;
using Katalog.Api.Contracts;
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
        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/labels", new { name = "Ninja Tune" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LabelSummaryResponse>();
        Assert.NotNull(created);
        Assert.Equal("ninja-tune", created!.Slug);

        // Duplicate slug -> 409
        var duplicateResponse = await client.PostAsJsonAsync("/api/labels", new { name = "Ninja Tune" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        // List
        var labels = await client.GetFromJsonAsync<LabelSummaryResponse[]>("/api/labels");
        Assert.NotNull(labels);
        var label = Assert.Single(labels);
        Assert.Equal("Ninja Tune", label.Name);

        // Detail
        var detail = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{created.Id}");
        Assert.Equal(0, detail!.ArtistCount);
        Assert.Empty(detail!.Artists);

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
    public async Task AddArtistToLabel_LinksArtist_AndDetailShowsIt()
    {
        spotify.Reset();
        spotify.StubTokenExchange();
        spotify.Server.Given(Request.Create().WithPath("/v1/artists/artistone").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(WireMockSpotify.ArtistJson("artistone", "Fever Ray")));

        await using var factory = new KatalogApiFactory(postgres, spotify);
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var labelResponse = await client.PostAsJsonAsync("/api/labels", new { name = "Rabid Records" });
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

        // Remove artist from label
        var removeResponse = await client.DeleteAsync($"/api/labels/{label.Id}/artists/{linked.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        var detailAfterRemove = await client.GetFromJsonAsync<LabelDetailResponse>($"/api/labels/{label.Id}");
        Assert.Equal(0, detailAfterRemove!.ArtistCount);
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
}
