using Katalog.Api.Domain;
using Katalog.Api.Infrastructure;
using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Features.Releases.Polling;

/// <summary>
/// Polls Spotify for albums matching each followed label via the <c>label:"&lt;name&gt;"</c>
/// search filter and upserts them idempotently (ON CONFLICT (spotify_id)). The result is
/// crash-safe because a poll cursor row records the last successful run and re-running the same
/// data is a no-op update.
/// </summary>
/// <remarks>
/// Spotify's label search filter matches fuzzily (following "Globuli" also returns albums
/// from near-miss labels such as "Globulin"), so each candidate album is verified against the
/// full album object (GET /albums/{id} - one GET per candidate, the captain's explicit quota
/// choice): only albums whose real label exactly (case-insensitive, trimmed) equals the
/// followed label's name are upserted, and the stored <c>label_spotify</c> attribution is the
/// album's real Spotify label, not the discovering label's name. When verification finds a
/// positive real-label mismatch for an album that is already linked to the label, the existing
/// junction link is removed, so releases whose real label is not exactly the followed one can
/// never stay listed - including links written before exact verification existed, and links
/// invalidated by a label rename (the rename-time link audit re-verifies every existing link).
/// </remarks>
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
                    await VerifyAndUpsertAlbumAsync(label, album, cancellationToken);
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
                await VerifyAndUpsertAlbumAsync(label, album, cancellationToken);
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

    /// <summary>
    /// Audits every existing label_albums link of a label against the label's (possibly new)
    /// name — used before a rename commits, which deliberately retargets the label and can
    /// invalidate links that the discovery search will never re-encounter under the new name.
    /// For each linked album the real Spotify label is fetched via the same GET /albums/{id}
    /// verification discovery uses: a positively verified mismatch unlinks the album; an exact
    /// match re-upserts it, correcting any stale <c>label_spotify</c> attribution; a 404 means
    /// the album no longer exists in Spotify's catalog (a verified upstream fact, never an
    /// exact-match candidate), so its stale link is removed. Any other unverifiable link -
    /// album GET failure, 5xx/429, cancellation, or an album reporting no label - aborts the
    /// audit with <see cref="LabelLinkAuditIncompleteException"/> so the caller can roll back
    /// instead of silently completing a rename with unverified links. Runs inside the caller's
    /// transaction when one is active (all its DB writes are raw SQL on the shared connection).
    /// </summary>
    public async Task AuditLabelLinksAsync(Guid labelId, string labelName, CancellationToken cancellationToken)
    {
        var links = await context.LabelAlbums
            .AsNoTracking()
            .Where(la => la.LabelId == labelId)
            .Join(context.Albums, la => la.AlbumId, a => a.Id,
                (la, a) => new { a.Id, a.SpotifyId, a.Name })
            .ToListAsync(cancellationToken);

        var label = new Label { Id = labelId, Name = labelName };

        foreach (var link in links)
        {
            SpotifyAlbumItem? fullAlbum;
            try
            {
                fullAlbum = await spotifyApiClient.GetAlbumAsync(link.SpotifyId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Link audit could not verify album {AlbumId} under label {LabelName} ({LabelId}); the audit must not complete.",
                    link.SpotifyId, labelName, labelId);
                throw new LabelLinkAuditIncompleteException(labelId, link.SpotifyId, "Spotify verification failed",
                    ex);
            }

            if (fullAlbum is null)
            {
                await DeleteLabelAlbumAsync(link.SpotifyId, labelId, cancellationToken);
                logger.LogInformation(
                    "Link audit removed album {AlbumId} ({AlbumName}) under label {LabelName}: Spotify reports the album no longer exists.",
                    link.SpotifyId, link.Name, labelName);
                continue;
            }

            var realLabel = fullAlbum.Label?.Trim();
            if (string.IsNullOrWhiteSpace(realLabel))
            {
                logger.LogInformation(
                    "Link audit could not verify album {AlbumId} ({AlbumName}) under label {LabelName}: Spotify reported no label to verify.",
                    link.SpotifyId, link.Name, labelName);
                throw new LabelLinkAuditIncompleteException(labelId, link.SpotifyId, "Spotify reported no label");
            }

            if (!string.Equals(realLabel, labelName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await RemoveVerifiedMismatchedLabelAlbumAsync(link.SpotifyId, labelId, labelName, realLabel,
                    cancellationToken);
                continue;
            }

            await UpsertAlbumAsync(label, fullAlbum, realLabel, cancellationToken);
        }

        logger.LogInformation("Link audit completed for label {LabelName} ({LabelId}) over {LinkCount} link(s).",
            labelName, labelId, links.Count);
    }

    /// <summary>
    /// Verifies a search candidate against its real Spotify label before upserting: the
    /// label:"..." search filter matches fuzzily (near-miss labels like "Globulin" for a
    /// followed "Globuli"), so a candidate is only upserted when its full album object's
    /// <c>label</c> exactly equals the followed label's name (case-insensitive, trimmed).
    /// A candidate that cannot be verified (album GET fails or reports no label) is skipped;
    /// the next poll cycle re-fetches and re-verifies it. A verified mismatch removes any
    /// existing link between the album and the label (self-heal), so a release never stays
    /// listed under a label whose real label is not exactly the followed one. The stored
    /// attribution is the album's real Spotify label.
    /// </summary>
    private async Task VerifyAndUpsertAlbumAsync(Label label, SpotifyAlbumItem album,
        CancellationToken cancellationToken)
    {
        var fullAlbum = await spotifyApiClient.GetAlbumAsync(album.Id, cancellationToken);
        var realLabel = fullAlbum?.Label?.Trim();

        if (string.IsNullOrWhiteSpace(realLabel))
        {
            logger.LogInformation(
                "Skipping album {AlbumId} ({AlbumName}) for label {LabelName}: Spotify reported no label to verify.",
                album.Id, album.Name, label.Name);
            return;
        }

        if (!string.Equals(realLabel, label.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            await RemoveVerifiedMismatchedLabelAlbumAsync(album.Id, label.Id, label.Name, realLabel,
                cancellationToken);
            return;
        }

        await UpsertAlbumAsync(label, album, realLabel, cancellationToken);
    }

    /// <summary>
    /// Removes an album's junction link to a label after a positively verified real-label
    /// mismatch (the album GET succeeded and reported a label that is not exactly the
    /// followed one). This is the only deletion path for label_albums rows: a candidate that
    /// cannot be verified, or that search no longer returns, never causes a deletion. For
    /// links created before exact verification existed, poll cycles self-heal because every
    /// discovery query re-encounters contaminated albums.
    /// </summary>
    private async Task RemoveVerifiedMismatchedLabelAlbumAsync(string spotifyAlbumId, Guid labelId,
        string labelName, string realLabel, CancellationToken cancellationToken)
    {
        var removed = await DeleteLabelAlbumAsync(spotifyAlbumId, labelId, cancellationToken);
        if (removed > 0)
        {
            logger.LogInformation(
                "Removed mismatched release {AlbumId} from label {LabelName}: Spotify's real label {RealLabel} is not an exact match.",
                spotifyAlbumId, labelName, realLabel);
        }
        else
        {
            logger.LogDebug(
                "Verified label mismatch for album {AlbumId} against label {LabelName} (real label {RealLabel}); no existing link to remove.",
                spotifyAlbumId, labelName, realLabel);
        }
    }

    /// <summary>
    /// Deletes the junction row for one album-label pair (on the caller's transaction when one
    /// is active). Returns the number of rows removed.
    /// </summary>
    private async Task<int> DeleteLabelAlbumAsync(string spotifyAlbumId, Guid labelId,
        CancellationToken cancellationToken)
    {
        await using var command = await PrepareCommandAsync(cancellationToken);
        command.CommandText = """
            DELETE FROM label_albums la
            USING albums a
            WHERE la.album_id = a.id
              AND la.label_id = @labelId
              AND a.spotify_id = @spotifyId
            """;

        command.Parameters.Add(P("labelId", labelId));
        command.Parameters.Add(P("spotifyId", spotifyAlbumId));

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Opens the shared DB connection if needed and creates a command on it, binding the
    /// active EF transaction so raw-SQL writes commit/roll back together with the callers'
    /// EF changes.
    /// </summary>
    private async Task<System.Data.Common.DbCommand> PrepareCommandAsync(CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        if (context.Database.CurrentTransaction is { } transaction)
            command.Transaction = transaction.GetDbTransaction();

        return command;
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
    /// (research §2.5, arch §3.4). <paramref name="spotifyLabel"/> is the album's real,
    /// verified Spotify label - used as the stored attribution, so a discovered release
    /// always shows the label Spotify itself reports (not the discovering label's name).
    /// </summary>
    private async Task UpsertAlbumAsync(Label label, SpotifyAlbumItem album, string spotifyLabel,
        CancellationToken cancellationToken)
    {
        var albumType = ParseAlbumType(album.AlbumType);
        var (releaseDate, precision) = ParseReleaseDate(album.ReleaseDate, album.ReleaseDatePrecision);

        await using var command = await PrepareCommandAsync(cancellationToken);
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
                    label_spotify = EXCLUDED.label_spotify,
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
        command.Parameters.Add(P("labelSpotify", spotifyLabel));
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
        await using var command = await PrepareCommandAsync(cancellationToken);
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
        await using var command = await PrepareCommandAsync(cancellationToken);
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
    /// Links are add/confirm only here: a transient search miss never deletes a legitimately
    /// discovered album. Deletion happens solely in
    /// <see cref="RemoveVerifiedMismatchedLabelAlbumAsync"/> when verification positively
    /// proves the album's real label is not exactly the followed label's name.
    /// </summary>
    private async Task UpsertLabelAlbumAsync(Guid albumId, Guid labelId, CancellationToken cancellationToken)
    {
        await using var command = await PrepareCommandAsync(cancellationToken);
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
        await using var command = await PrepareCommandAsync(cancellationToken);
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
