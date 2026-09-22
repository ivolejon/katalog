using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Setup;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Features.Artists;

public sealed class SearchArtists(ISpotifyApiClient spotifyApiClient, IOptions<SpotifyOptions> spotifyOptions)
{
    public async Task<IReadOnlyList<ArtistSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var response = await spotifyApiClient.SearchArtistsAsync(
            query, limit, spotifyOptions.Value.Market, cancellationToken);

        return response.Artists.Items
            .Select(i => new ArtistSearchResult(
                i.Id,
                i.Name,
                i.ImageUrl,
                i.ExternalUrl,
                i.Genres ?? [],
                i.Popularity))
            .ToList();
    }
}
