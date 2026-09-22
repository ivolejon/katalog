using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Features.Releases.Polling;

/// <summary>
/// Polls the Spotify artist discography for every artist linked to at least one label and
/// upserts albums idempotently (ON CONFLICT (spotify_id), arch report §3.4/§3.7). The result is
/// crash-safe because a poll cursor row records the last successful run and re-running the same
/// data is a no-op update.
/// </summary>
public sealed class ReleasePoller(
    KatalogContext context,
    ISpotifyApiClient spotifyApiClient,
    ILogger<ReleasePoller> logger,
    TimeProvider timeProvider,
    IOptions<SpotifyOptions> spotifyOptions)
{
    public const string JobName = "artist_new_releases";

    public async Task<IReadOnlyList<SpotifyAlbumItem>> FetchArtistAlbumsAsync(string spotifyArtistId,
        CancellationToken cancellationToken)
    {
        return await spotifyApiClient.GetArtistAlbumsAsync(
            spotifyArtistId, SpotifyOptions.AlbumsLimitMax, spotifyOptions.Value.Market, cancellationToken);
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        // Only artists that are followed under at least one label are polled; this keeps the
        // dev-mode quota small (research §2.7/§2.8).
        var artists = await context.Artists
            .Where(a => a.LabelArtists.Any())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var cursor = await GetOrCreateCursorAsync(cancellationToken);
        var startedAt = timeProvider.GetUtcNow();

        foreach (var artist in artists)
        {
            try
            {
                var albums = await FetchArtistAlbumsAsync(artist.SpotifyId, cancellationToken);
                foreach (var album in albums)
                {
                    await UpsertAlbumAsync(artist.Id, album, cancellationToken);
                }

                logger.LogDebug("Polled {AlbumCount} albums for artist {ArtistName} ({SpotifyId}).",
                    albums.Count, artist.Name, artist.SpotifyId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A single artist must not kill the whole poll cycle; the next interval retries.
                logger.LogError(ex, "Release polling failed for artist {ArtistName} ({SpotifyId}).",
                    artist.Name, artist.SpotifyId);
            }
        }

        cursor.CursorValue = startedAt;
        cursor.LastRunAt = timeProvider.GetUtcNow();
        cursor.Status = "completed";
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Release poll completed for {ArtistCount} artist(s).", artists.Count);
    }

    private async Task<PollCursor> GetOrCreateCursorAsync(CancellationToken cancellationToken)
    {
        var cursor = await context.PollCursors.FindAsync([JobName], cancellationToken);
        if (cursor is not null)
            return cursor;

        cursor = new PollCursor { JobName = JobName };
        context.PollCursors.Add(cursor);
        return cursor;
    }

    /// <summary>
    /// Idempotent album upsert: INSERT ... ON CONFLICT (spotify_id) DO UPDATE, returning the
    /// album id so the album_artists junction row can be written (research §2.5, arch §3.4).
    /// </summary>
    private async Task UpsertAlbumAsync(Guid artistId, SpotifyAlbumItem album, CancellationToken cancellationToken)
    {
        var albumType = ParseAlbumType(album.AlbumType);
        var (releaseDate, precision) = ParseReleaseDate(album.ReleaseDate, album.ReleaseDatePrecision);

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH upserted AS (
                INSERT INTO albums (
                    id, spotify_id, name, album_type, release_date, release_date_precision,
                    label_spotify, label_id, image_url, external_url, total_tracks,
                    created_at_utc, updated_at_utc)
                VALUES (
                    @id, @spotifyId, @name, @albumType, @releaseDate, @releasePrecision,
                    @labelSpotify, NULL, @imageUrl, @externalUrl, @totalTracks,
                    now(), now())
                ON CONFLICT (spotify_id) DO UPDATE SET
                    name = EXCLUDED.name,
                    album_type = EXCLUDED.album_type,
                    release_date = EXCLUDED.release_date,
                    release_date_precision = EXCLUDED.release_date_precision,
                    label_spotify = COALESCE(EXCLUDED.label_spotify, albums.label_spotify),
                    image_url = EXCLUDED.image_url,
                    external_url = EXCLUDED.external_url,
                    total_tracks = EXCLUDED.total_tracks,
                    updated_at_utc = now()
                RETURNING id
            )
            SELECT id FROM upserted
            """;

        var albumId = Guid.CreateVersion7();
        command.Parameters.Add(P("id", albumId));
        command.Parameters.Add(P("spotifyId", album.Id));
        command.Parameters.Add(P("name", album.Name));
        command.Parameters.Add(P("albumType", (int)albumType));
        command.Parameters.Add(P("releaseDate", releaseDate));
        command.Parameters.Add(P("releasePrecision", (int)precision));
        command.Parameters.Add(P("labelSpotify", null));
        command.Parameters.Add(P("imageUrl", album.ImageUrl));
        command.Parameters.Add(P("externalUrl", album.ExternalUrl));
        command.Parameters.Add(P("totalTracks", album.TotalTracks));

        var idResult = await command.ExecuteScalarAsync(cancellationToken);
        var storedAlbumId = idResult is Guid stored ? stored : albumId;

        await UpsertAlbumArtistAsync(storedAlbumId, artistId, cancellationToken);
    }

    private async Task UpsertAlbumArtistAsync(Guid albumId, Guid artistId, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO album_artists (album_id, artist_id, position)
            VALUES (@albumId, @artistId, @position)
            ON CONFLICT (album_id, artist_id) DO UPDATE SET position = EXCLUDED.position
            """;

        command.Parameters.Add(P("albumId", albumId));
        command.Parameters.Add(P("artistId", artistId));
        command.Parameters.Add(P("position", 0));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Builds a parameter, mapping null to DBNull (avoids ambiguous NpgsqlParameter ctors).</summary>
    private static Npgsql.NpgsqlParameter P(string name, object? value) => new()
    {
        ParameterName = name,
        Value = value ?? DBNull.Value
    };

    internal static AlbumType ParseAlbumType(string? albumType) => albumType switch
    {
        "album" => AlbumType.Album,
        "single" => AlbumType.Single,
        "compilation" => AlbumType.Compilation,
        _ => AlbumType.Album
    };

    /// <summary>
    /// Normalizes Spotify's release_date (precision year/month/day) into a DateOnly: coarser
    /// precisions are stored as the first day of the period (research §2.5/§5).
    /// </summary>
    internal static (DateOnly? Date, ReleaseDatePrecision Precision) ParseReleaseDate(string? releaseDate, string? precision)
    {
        var parsedPrecision = precision switch
        {
            "year" => ReleaseDatePrecision.Year,
            "month" => ReleaseDatePrecision.Month,
            "day" => ReleaseDatePrecision.Day,
            _ => ReleaseDatePrecision.Day
        };

        if (string.IsNullOrWhiteSpace(releaseDate))
            return (null, parsedPrecision);

        return parsedPrecision switch
        {
            ReleaseDatePrecision.Year when int.TryParse(releaseDate, out var year) => (new DateOnly(year, 1, 1), parsedPrecision),
            ReleaseDatePrecision.Month when DateOnly.TryParseExact(releaseDate, "yyyy-MM", out var month) => (month, parsedPrecision),
            _ when DateOnly.TryParseExact(releaseDate, "yyyy-MM-dd", out var day) => (day, parsedPrecision),
            _ => (null, parsedPrecision)
        };
    }
}
