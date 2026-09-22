namespace Katalog.Api.Domain;

/// <summary>
/// Album category as used by Spotify's <c>album_type</c> field. Stored as int with gaps
/// (arch report §3.4: enums as int, never string conversions).
/// </summary>
public enum AlbumType
{
    Album = 10,
    Single = 20,
    Compilation = 30,
}

/// <summary>
/// Precision of a release date: year/month/day. Stored as int so releases with coarser
/// precision sort correctly (research §5).
/// </summary>
public enum ReleaseDatePrecision
{
    Year = 10,
    Month = 20,
    Day = 30,
}

/// <summary>
/// How an artist-label link came to exist. Stored as int with gaps so future sources
/// (e.g. MusicBrainz) can be added without renumbering (firstmate spec: 10=manual,
/// 20=album_metadata).
/// </summary>
public enum Provenance
{
    Manual = 10,
    AlbumMetadata = 20,
}
