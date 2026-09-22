namespace Katalog.Api.Domain;

/// <summary>
/// A Spotify artist, mirrored into our database. <see cref="SpotifyId"/> is the natural
/// key used for idempotent upserts; releases are polled per artist (research §2.5).
/// </summary>
public sealed class Artist
{
    public Guid Id { get; set; }
    public string SpotifyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public string[]? Genres { get; set; }
    public int? Popularity { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<LabelArtist> LabelArtists { get; } = [];
    public List<AlbumArtist> AlbumArtists { get; } = [];
}
