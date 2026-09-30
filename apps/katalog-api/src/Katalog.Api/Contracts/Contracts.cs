namespace Katalog.Api.Contracts;

public sealed record LabelSummaryResponse(
    Guid Id,
    IReadOnlyList<string> SpotifyIds,
    string Name,
    string Slug,
    int ArtistCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LabelDetailResponse(
    Guid Id,
    IReadOnlyList<string> SpotifyIds,
    string Name,
    string Slug,
    int ArtistCount,
    int ReleaseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ArtistSummaryResponse> Artists,
    IReadOnlyList<AlbumResponse> Releases);

public sealed record ArtistSummaryResponse(
    Guid Id,
    string SpotifyId,
    string Name,
    string? ImageUrl,
    string? ExternalUrl,
    string[]? Genres,
    int? Popularity);

public sealed record ArtistSearchResult(
    string Id,
    string Name,
    string? ImageUrl,
    string? ExternalUrl,
    IReadOnlyList<string> Genres,
    int? Popularity);

/// <summary>Album hit from a Spotify label search (label:"..." filter, type=album).</summary>
public sealed record LabelSearchAlbumResult(
    string AlbumId,
    string Name,
    IReadOnlyList<LabelSearchArtistResult> Artists,
    string? ImageUrl,
    string? ReleaseDate,
    string? ExternalUrl);

/// <summary>Artist on a label-search album hit (simplified: id + name from the album object).</summary>
public sealed record LabelSearchArtistResult(string SpotifyId, string Name);

/// <summary>
/// A label hit from a Spotify label search. The name is the searched term (simplified album
/// objects carry no label field), and the albums are the hits used to build artist anchors.
/// </summary>
public sealed record LabelSearchResult(
    string Name,
    IReadOnlyList<LabelSearchAlbumResult> Albums);

/// <summary>
/// Label search result: the normalized query plus the matching label hits. Because Spotify has
/// no searchable label resource, the API searches albums via <c>label:"&lt;name&gt;"</c> and
/// returns the searched label as a single hit.
/// </summary>
public sealed record LabelSearchResponse(
    string Query,
    IReadOnlyList<LabelSearchResult> Labels);

/// <summary>Album/release row used by the frontend "Releaser" tab.</summary>
public sealed record AlbumResponse(
    Guid Id,
    string SpotifyId,
    string Name,
    string AlbumType,
    string? ReleaseDate,
    string ReleaseDatePrecision,
    string? LabelSpotify,
    string? ImageUrl,
    string? ExternalUrl,
    int TotalTracks,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<string> ArtistSpotifyIds);

/// <summary>Paged releases for a label. The database is the source of truth; no Spotify calls are made per page.</summary>
public sealed record LabelReleasesResponse(
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore,
    IReadOnlyList<AlbumResponse> Releases);

public sealed record CreateLabelRequest(string Name, IReadOnlyList<string> SpotifyIds);

public sealed record UpdateLabelRequest(string Name);

public sealed record AddArtistToLabelRequest(string SpotifyArtistId);
