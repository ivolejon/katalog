using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Setup;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Features.Labels;

/// <summary>
/// Searches Spotify for albums matching a record label via the undocumented-but-working
/// <c>label:"&lt;name&gt;"</c> search filter (verified live 2026-09-22; not in the official
/// spec's filter list). Simplified album objects carry no label field, so the label name is
/// the searched term and the artists come from each hit's album.
/// </summary>
public sealed class SearchLabels(ISpotifyApiClient spotifyApiClient, IOptions<SpotifyOptions> spotifyOptions)
{
    public async Task<LabelSearchResponse> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var response = await spotifyApiClient.SearchAlbumsByLabelAsync(
            query, limit, spotifyOptions.Value.Market, cancellationToken);

        var albums = response.Albums.Items
            .Select(a => new LabelSearchAlbumResult(
                a.Id,
                a.Name,
                (a.Artists ?? []).Select(ar => new LabelSearchArtistResult(ar.Id, ar.Name)).ToList(),
                a.ImageUrl,
                a.ReleaseDate,
                a.ExternalUrl))
            .ToList();

        return new LabelSearchResponse(query, albums);
    }
}