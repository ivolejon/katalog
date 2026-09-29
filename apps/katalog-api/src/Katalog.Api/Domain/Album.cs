namespace Katalog.Api.Domain;

/// <summary>
/// A Spotify album/release. No tracks are stored (captain's decision). <see cref="LabelSpotify"/>
/// is the album's real Spotify label (deprecated field, still available on the full album object),
/// used as the ground truth for exact matching and stored as the release attribution.
/// <see cref="LabelId"/> is the app-owned label the album was last discovered/verified for.
/// </summary>
public sealed class Album
{
    public Guid Id { get; set; }
    public string SpotifyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AlbumType AlbumType { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public ReleaseDatePrecision ReleaseDatePrecision { get; set; }
    public string? LabelSpotify { get; set; }
    public Guid? LabelId { get; set; }
    public string? ImageUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public int TotalTracks { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<AlbumArtist> AlbumArtists { get; } = [];
    public List<LabelAlbum> LabelAlbums { get; } = [];
}
