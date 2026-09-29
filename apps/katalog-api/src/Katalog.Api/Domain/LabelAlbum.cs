namespace Katalog.Api.Domain;

/// <summary>
/// Junction table between labels and albums (many-to-many). Exact real-label verification
/// means an album is only linked to the followed label whose name matches the album's real
/// Spotify label; the junction still models the relationship independently of the denormalized
/// album.label_id.
/// </summary>
public sealed class LabelAlbum
{
    public Guid LabelId { get; set; }
    public Guid AlbumId { get; set; }
    public DateTimeOffset FirstSeenAtUtc { get; set; }
    public DateTimeOffset LastConfirmedAtUtc { get; set; }

    public Label Label { get; set; } = null!;
    public Album Album { get; set; } = null!;
}
