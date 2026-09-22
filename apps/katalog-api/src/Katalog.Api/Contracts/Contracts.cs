namespace Katalog.Api.Contracts;

public sealed record LabelSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    int ArtistCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LabelDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    int ArtistCount,
    int ReleaseCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ArtistSummaryResponse> Artists);

public sealed record ArtistSummaryResponse(
    Guid Id,
    string SpotifyId,
    string Name,
    string? ImageUrl,
    string? ExternalUrl,
    int? Popularity);

public sealed record ArtistSearchResult(
    string SpotifyId,
    string Name,
    string? ImageUrl,
    string? ExternalUrl,
    IReadOnlyList<string> Genres,
    int? Popularity);

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

public sealed record CreateLabelRequest(string Name);

public sealed record UpdateLabelRequest(string Name);

public sealed record AddArtistToLabelRequest(string SpotifyArtistId);
