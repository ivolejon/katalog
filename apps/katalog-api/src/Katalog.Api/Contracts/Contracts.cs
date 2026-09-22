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
/// Label search result: the normalized label name that was searched plus the matching albums.
/// The label name shown to the user is the searched term (simplified album objects carry no
/// label field), so <see cref="MatchedLabelName"/> echoes the query.
/// </summary>
public sealed record LabelSearchResponse(
    string MatchedLabelName,
    IReadOnlyList<LabelSearchAlbumResult> Albums);

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
    IReadOnlyList<string> ArtistNames);

public sealed record CreateLabelRequest(string Name, IReadOnlyList<string>? SpotifyIds);

public sealed record UpdateLabelRequest(string Name);

public sealed record AddArtistToLabelRequest(string SpotifyArtistId);
