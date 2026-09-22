namespace Katalog.Api.Domain;

/// <summary>
/// Junction table between albums and artists, replacing the jsonb column proposed in research
/// (arch report §3.4: no JSON columns, junction tables for many-to-many).
/// </summary>
public sealed class AlbumArtist
{
    public Guid AlbumId { get; set; }
    public Guid ArtistId { get; set; }
    public int Position { get; set; }

    public Album Album { get; set; } = null!;
    public Artist Artist { get; set; } = null!;
}
