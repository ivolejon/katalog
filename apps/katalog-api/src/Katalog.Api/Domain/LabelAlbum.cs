namespace Katalog.Api.Domain;

/// <summary>
/// Junction table between labels and albums (many-to-many). A release may legitimately belong
/// to more than one followed label, so the link is stored per discovery instead of overwriting
/// a single album.label_id.
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
