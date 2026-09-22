using Katalog.Api.Contracts;
using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Katalog.Api.Infrastructure.Spotify;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Artists;

public sealed class AddArtistToLabel(
    KatalogContext context,
    ISpotifyApiClient spotifyApiClient,
    TimeProvider timeProvider,
    ILogger<AddArtistToLabel> logger)
{
    /// <summary>
    /// Links a Spotify artist to a label (manual provenance). The artist is mirrored from Spotify
    /// (upserted on spotify_id) and the artist_label junction row is created or refreshed.
    /// Returns null when the label does not exist or Spotify does not know the artist.
    /// </summary>
    public async Task<ArtistSummaryResponse?> AddAsync(Guid labelId, string spotifyArtistId, CancellationToken cancellationToken)
    {
        var labelExists = await context.Labels.AnyAsync(l => l.Id == labelId, cancellationToken);
        if (!labelExists)
            return null;

        var spotifyArtist = await spotifyApiClient.GetArtistAsync(spotifyArtistId, cancellationToken);
        if (spotifyArtist is null)
            return null;

        var now = timeProvider.GetUtcNow();
        var artist = await context.Artists.FirstOrDefaultAsync(a => a.SpotifyId == spotifyArtistId, cancellationToken);
        if (artist is null)
        {
            artist = new Artist
            {
                Id = Guid.CreateVersion7(),
                SpotifyId = spotifyArtist.Id,
                Name = spotifyArtist.Name,
                ImageUrl = spotifyArtist.ImageUrl,
                ExternalUrl = spotifyArtist.ExternalUrl,
                Genres = spotifyArtist.Genres?.ToArray(),
                Popularity = spotifyArtist.Popularity,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            context.Artists.Add(artist);
        }
        else
        {
            artist.Name = spotifyArtist.Name;
            artist.ImageUrl = spotifyArtist.ImageUrl;
            artist.ExternalUrl = spotifyArtist.ExternalUrl;
            artist.Genres = spotifyArtist.Genres?.ToArray();
            artist.Popularity = spotifyArtist.Popularity;
            artist.UpdatedAtUtc = now;
        }

        var link = await context.LabelArtists.FindAsync([labelId, artist.Id], cancellationToken);
        if (link is null)
        {
            context.LabelArtists.Add(new LabelArtist
            {
                LabelId = labelId,
                ArtistId = artist.Id,
                Provenance = Provenance.Manual,
                FirstSeenAtUtc = now,
                LastConfirmedAtUtc = now
            });
        }
        else
        {
            link.LastConfirmedAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Linked Spotify artist {SpotifyId} ({Name}) to label {LabelId} (manual).",
            artist.SpotifyId, artist.Name, labelId);

        return new ArtistSummaryResponse(artist.Id, artist.SpotifyId, artist.Name, artist.ImageUrl, artist.ExternalUrl, artist.Popularity);
    }
}
