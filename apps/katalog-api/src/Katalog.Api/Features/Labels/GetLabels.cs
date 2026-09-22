using Katalog.Api.Contracts;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Labels;

public sealed class GetLabels(KatalogContext context)
{
    public async Task<IReadOnlyList<LabelSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.Labels
            .OrderBy(l => l.Name)
            .Select(l => new LabelSummaryResponse(
                l.Id,
                l.Name,
                l.Slug,
                l.LabelArtists.Count,
                l.CreatedAtUtc,
                l.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<LabelDetailResponse?> GetDetailAsync(Guid labelId, CancellationToken cancellationToken)
    {
        return await context.Labels
            .Where(l => l.Id == labelId)
            .Select(l => new LabelDetailResponse(
                l.Id,
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
                        la.Artist.Popularity))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
