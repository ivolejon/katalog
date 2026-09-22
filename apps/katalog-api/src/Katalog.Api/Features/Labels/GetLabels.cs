using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure;
using Katalog.Api.Features.Releases;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Labels;

public sealed class GetLabels(KatalogContext context, GetLabelReleases getLabelReleases)
{
    public async Task<IReadOnlyList<LabelSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.Labels
            .OrderBy(l => l.Name)
            .Select(l => new LabelSummaryResponse(
                l.Id,
                l.LabelArtists.OrderBy(la => la.Artist.Name).Select(la => la.Artist.SpotifyId).ToList(),
                l.Name,
                l.Slug,
                l.LabelArtists.Count,
                l.CreatedAtUtc,
                l.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<LabelDetailResponse?> GetDetailAsync(Guid labelId, CancellationToken cancellationToken)
    {
        var detail = await context.Labels
            .Where(l => l.Id == labelId)
            .Select(l => new LabelDetailResponse(
                l.Id,
                l.LabelArtists.OrderBy(la => la.Artist.Name).Select(la => la.Artist.SpotifyId).ToList(),
                l.Name,
                l.Slug,
                l.LabelArtists.Count,
                l.LabelArtists.SelectMany(la => la.Artist.AlbumArtists.Select(aa => aa.AlbumId)).Distinct().Count(),
                l.CreatedAtUtc,
                l.UpdatedAtUtc,
                l.LabelArtists
                    .OrderBy(la => la.Artist.Name)
                    .Select(la => new ArtistSummaryResponse(
                        la.Artist.Id,
                        la.Artist.SpotifyId,
                         la.Artist.Name,
                         la.Artist.ImageUrl,
                         la.Artist.ExternalUrl,
                         la.Artist.Genres,
                         la.Artist.Popularity))
                 .ToList(),
                  null!))
            .SingleOrDefaultAsync(cancellationToken);

        if (detail is null)
            return null;

        var releases = await getLabelReleases.ListAsync(labelId, cancellationToken) ?? [];
        return detail with { Releases = releases };
    }
}
