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
        // maxItems caps pagination at the caller's limit: the search endpoint only ever shows
        // `limit` albums, so exhausting a well-known label's full result set would burn quota.
        var searchResults = await spotifyApiClient.SearchAlbumsByLabelAsync(
            query, limit, spotifyOptions.Value.Market, maxItems: limit, cancellationToken);

        var albums = searchResults
            .Take(limit)
            .Select(a => new LabelSearchAlbumResult(
                a.Id,
                a.Name,
                (a.Artists ?? []).Select(ar => new LabelSearchArtistResult(ar.Id, ar.Name)).ToList(),
                a.ImageUrl,
                a.ReleaseDate,
                a.ExternalUrl))
            .ToList();

        // Spotify has no label resource, so the searched term is the label name. Return it as a
        // single label hit with the matching albums so the frontend can list labels and still
        // build artist anchors from the albums.
        var label = new LabelSearchResult(query, albums);
        return new LabelSearchResponse(query, [label]);
    }
}