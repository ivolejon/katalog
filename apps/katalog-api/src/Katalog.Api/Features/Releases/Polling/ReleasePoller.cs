using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Features.Releases.Polling;

/// <summary>
/// Polls Spotify for albums matching each followed label via the <c>label:"&lt;name&gt;"</c>
/// search filter and upserts them idempotently (ON CONFLICT (spotify_id)). The result is
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
    // The cursor primary key predates the switch from artist-discography polling to label-search
    // polling. Keeping the name avoids a cursor migration; the poller now discovers releases by
    // label instead of by artist.
    public const string JobName = "artist_new_releases";

    public async Task<IReadOnlyList<SpotifyAlbumItem>> FetchLabelAlbumsAsync(string labelName,
        CancellationToken cancellationToken)
    {
        return await spotifyApiClient.SearchAlbumsByLabelAsync(
            labelName, SpotifyOptions.SearchLimitMax, spotifyOptions.Value.Market, maxItems: null, cancellationToken);
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var labels = await context.Labels
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var cursor = await GetOrCreateCursorAsync(cancellationToken);
        var startedAt = timeProvider.GetUtcNow();

        foreach (var label in labels)
        {
            try
            {
                var albums = await FetchLabelAlbumsAsync(label.Name, cancellationToken);
                foreach (var album in albums)
                {
                    await UpsertAlbumAsync(label, album, cancellationToken);
                }

                logger.LogDebug("Polled {AlbumCount} albums for label {LabelName} ({LabelId}).",
                    albums.Count, label.Name, label.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A single label must not kill the whole poll cycle; the next interval retries.
                logger.LogError(ex, "Release polling failed for label {LabelName} ({LabelId}).",
                    label.Name, label.Id);
            }
        }

        cursor.CursorValue = startedAt;
        cursor.LastRunAt = timeProvider.GetUtcNow();
        cursor.Status = "completed";
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Release poll completed for {LabelCount} label(s).", labels.Count);
    }

    /// <summary>
    /// Polls albums matching a single label immediately, upserting them idempotently.
    /// Used when a new label is followed so releases appear right away instead of waiting for
    /// the next scheduled poll cycle.
    /// </summary>
    public async Task PollLabelAsync(Guid labelId, CancellationToken cancellationToken)
    {
        var label = await context.Labels
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == labelId, cancellationToken);

        if (label is null)
        {
            logger.LogWarning("Immediate release poll requested for unknown label {LabelId}.", labelId);
            return;
        }

        try
        {
            var albums = await FetchLabelAlbumsAsync(label.Name, cancellationToken);
            foreach (var album in albums)
            {
                await UpsertAlbumAsync(label, album, cancellationToken);
            }

            logger.LogInformation("Immediate release poll completed for label {LabelId} with {AlbumCount} album(s).",
                labelId, albums.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A polling failure during label creation must not roll the label back; the next
            // scheduled poll cycle will retry.
            logger.LogError(ex, "Immediate release polling failed for label {LabelName} ({LabelId}).",
                label.Name, labelId);
        }
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
    /// album id so the album_artists and label_albums junction rows can be written
    /// (research §2.5, arch §3.4).
    /// </summary>
    private async Task UpsertAlbumAsync(Label label, SpotifyAlbumItem album, CancellationToken cancellationToken)
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
                    @labelSpotify, @labelId, @imageUrl, @externalUrl, @totalTracks,
                    now(), now())
                ON CONFLICT (spotify_id) DO UPDATE SET
                    name = EXCLUDED.name,
                    album_type = EXCLUDED.album_type,
                    release_date = EXCLUDED.release_date,
                    release_date_precision = EXCLUDED.release_date_precision,
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
        command.Parameters.Add(P("labelSpotify", label.Name));
        command.Parameters.Add(P("labelId", label.Id));
        command.Parameters.Add(P("imageUrl", album.ImageUrl));
        command.Parameters.Add(P("externalUrl", album.ExternalUrl));
        command.Parameters.Add(P("totalTracks", album.TotalTracks));

        var idResult = await command.ExecuteScalarAsync(cancellationToken);
        var storedAlbumId = idResult is Guid stored ? stored : albumId;

        await UpsertLabelAlbumAsync(storedAlbumId, label.Id, cancellationToken);

        if (album.Artists is not { Count: > 0 })
        {
            logger.LogWarning("Spotify returned album {AlbumId} ({AlbumName}) without artists; skipping artist linking.",
                album.Id, album.Name);
            await RemoveMissingAlbumArtistsAsync(storedAlbumId, [], cancellationToken);
            return;
        }

        var storedArtistIds = new List<Guid>(album.Artists.Count);
        for (var position = 0; position < album.Artists.Count; position++)
        {
            var albumArtist = album.Artists[position];
            var storedArtistId = await UpsertArtistAsync(albumArtist, cancellationToken);
            storedArtistIds.Add(storedArtistId);
            await UpsertAlbumArtistAsync(storedAlbumId, storedArtistId, position, cancellationToken);
        }

        await RemoveMissingAlbumArtistsAsync(storedAlbumId, storedArtistIds, cancellationToken);
    }

    private async Task<Guid> UpsertArtistAsync(SpotifyAlbumArtist artist, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO artists (id, spotify_id, name, created_at_utc, updated_at_utc)
            VALUES (@id, @spotifyId, @name, now(), now())
            ON CONFLICT (spotify_id) DO UPDATE SET
                name = EXCLUDED.name,
                updated_at_utc = now()
            RETURNING id
            """;

        var artistId = Guid.CreateVersion7();
        command.Parameters.Add(P("id", artistId));
        command.Parameters.Add(P("spotifyId", artist.Id));
        command.Parameters.Add(P("name", artist.Name));

        var idResult = await command.ExecuteScalarAsync(cancellationToken);
        return idResult is Guid stored ? stored : artistId;
    }

    private async Task UpsertAlbumArtistAsync(Guid albumId, Guid artistId, int position, CancellationToken cancellationToken)
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
        command.Parameters.Add(P("position", position));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Links a discovered album to the discovering label via the label_albums junction table.
    /// Existing links for other labels are left untouched, so an album that matches multiple
    /// followed labels appears under each of them.
    /// </summary>
    private async Task UpsertLabelAlbumAsync(Guid albumId, Guid labelId, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO label_albums (label_id, album_id, first_seen_at_utc, last_confirmed_at_utc)
            VALUES (@labelId, @albumId, now(), now())
            ON CONFLICT (label_id, album_id) DO UPDATE SET
                last_confirmed_at_utc = now()
            """;

        command.Parameters.Add(P("labelId", labelId));
        command.Parameters.Add(P("albumId", albumId));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RemoveMissingAlbumArtistsAsync(Guid albumId, IReadOnlyCollection<Guid> artistIds,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM album_artists
            WHERE album_id = @albumId
              AND artist_id <> ALL(@artistIds)
            """;

        command.Parameters.Add(P("albumId", albumId));
        command.Parameters.Add(P("artistIds", artistIds.ToArray()));

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
