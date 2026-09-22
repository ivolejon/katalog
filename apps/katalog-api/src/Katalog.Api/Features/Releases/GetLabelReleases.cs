using Katalog.Api.Contracts;
using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Releases;

public sealed class GetLabelReleases(KatalogContext context)
{
    /// <summary>
    /// Returns the releases for all artists under a label, newest first. NULL release dates sort
    /// last in Postgres by default for DESC, so they are coalesced to the minimum date.
    /// </summary>
    public async Task<IReadOnlyList<AlbumResponse>?> ListAsync(Guid labelId, CancellationToken cancellationToken)
    {
        var labelExists = await context.Labels.AnyAsync(l => l.Id == labelId, cancellationToken);
        if (!labelExists)
            return null;

        var rows = await context.Albums
            .Where(a => a.AlbumArtists.Any(aa => aa.Artist.LabelArtists.Any(la => la.LabelId == labelId)))
            .OrderByDescending(a => a.ReleaseDate ?? DateOnly.MinValue)
            .Select(a => new
            {
                a.Id,
                a.SpotifyId,
                a.Name,
                a.AlbumType,
                a.ReleaseDate,
                a.ReleaseDatePrecision,
                a.LabelSpotify,
                a.ImageUrl,
                a.ExternalUrl,
                a.TotalTracks,
                ArtistNames = a.AlbumArtists.Select(aa => aa.Artist.Name).Distinct().OrderBy(n => n).ToList()
            })
            .ToListAsync(cancellationToken);

        // Enum -> string mapping happens client-side; EF cannot translate arbitrary C# switches.
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
                r.ArtistNames))
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
