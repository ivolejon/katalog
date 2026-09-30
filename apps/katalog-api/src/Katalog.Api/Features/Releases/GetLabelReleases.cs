using Katalog.Api.Contracts;
using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Releases;

public sealed class GetLabelReleases(KatalogContext context)
{
    /// <summary>
    /// Returns the releases discovered for a label via the label_albums junction table,
    /// newest first. NULL release dates sort last in Postgres by default for DESC, so they
    /// are coalesced to the minimum date.
    /// </summary>
    public async Task<IReadOnlyList<AlbumResponse>?> ListAsync(Guid labelId, CancellationToken cancellationToken)
    {
        var labelExists = await context.Labels.AnyAsync(l => l.Id == labelId, cancellationToken);
        if (!labelExists)
            return null;

        var rows = await ReleasesQuery(labelId)
            .ToListAsync(cancellationToken);

        return MapRows(rows);
    }

    /// <summary>
    /// Returns a single page of releases for a label, plus the total count, a flag that
    /// indicates whether more pages exist, and a snapshot boundary. Paging is performed in
    /// the database; no Spotify calls are made per click. When snapshotBoundary is provided
    /// (echoed from a previous page), only albums with an id at or below the boundary are
    /// returned, so a release discovered between clicks cannot shift the window.
    /// </summary>
    public async Task<LabelReleasesResponse?> GetPageAsync(Guid labelId, int page, int pageSize,
        Guid? snapshotBoundary, CancellationToken cancellationToken)
    {
        var labelExists = await context.Labels.AnyAsync(l => l.Id == labelId, cancellationToken);
        if (!labelExists)
            return null;

        var totalCount = await context.LabelAlbums
            .CountAsync(la => la.LabelId == labelId, cancellationToken);

        var query = ReleasesQuery(labelId);
        if (snapshotBoundary.HasValue)
        {
            query = query.Where(a => a.Id <= snapshotBoundary.Value);
        }

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var releases = MapRows(rows);
        var hasMore = page * pageSize < totalCount;

        // On the first page, compute the snapshot boundary (max album id for the label).
        Guid? boundary = snapshotBoundary;
        if (!boundary.HasValue)
        {
            boundary = await context.Albums
                .Where(a => a.LabelAlbums.Any(la => la.LabelId == labelId))
                .OrderByDescending(a => a.Id)
                .Select(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new LabelReleasesResponse(page, pageSize, totalCount, hasMore, boundary, releases);
    }

    private IQueryable<Album> ReleasesQuery(Guid labelId)
    {
        return context.Albums
            .Include(a => a.AlbumArtists)
            .ThenInclude(aa => aa.Artist)
            .Where(a => a.LabelAlbums.Any(la => la.LabelId == labelId))
            .OrderByDescending(a => a.ReleaseDate ?? DateOnly.MinValue)
            .ThenBy(a => a.Id);
    }

    private static IReadOnlyList<AlbumResponse> MapRows(IReadOnlyList<Album> rows)
    {
        return rows
            .Select(r => new AlbumResponse(
                r.Id,
                r.SpotifyId,
                r.Name,
                MapAlbumType(r.AlbumType),
                r.ReleaseDate.HasValue ? r.ReleaseDate.Value.ToString("yyyy-MM-dd") : null,
                MapPrecision(r.ReleaseDatePrecision),
                r.LabelSpotify,
                r.ImageUrl,
                r.ExternalUrl,
                r.TotalTracks,
                r.AlbumArtists.Select(aa => aa.Artist.Name).Distinct().OrderBy(n => n).ToList(),
                r.AlbumArtists.Select(aa => aa.Artist.SpotifyId).ToList()))
            .ToList();
    }

    private static string MapAlbumType(AlbumType albumType) => albumType switch
    {
        AlbumType.Album => "album",
        AlbumType.Single => "single",
        AlbumType.Compilation => "compilation",
        _ => "album"
    };

    private static string MapPrecision(ReleaseDatePrecision precision) => precision switch
    {
        ReleaseDatePrecision.Year => "year",
        ReleaseDatePrecision.Month => "month",
        ReleaseDatePrecision.Day => "day",
        _ => "day"
    };
}
