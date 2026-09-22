namespace Katalog.Api.Domain;

/// <summary>
/// Junction table between artists and labels (many-to-many). <see cref="Provenance"/>
/// tracks how the link was created.
/// </summary>
public sealed class LabelArtist
{
    public Guid LabelId { get; set; }
    public Guid ArtistId { get; set; }
    public Provenance Provenance { get; set; }
    public DateTimeOffset FirstSeenAtUtc { get; set; }
    public DateTimeOffset LastConfirmedAtUtc { get; set; }

    public Label Label { get; set; } = null!;
    public Artist Artist { get; set; } = null!;
}
