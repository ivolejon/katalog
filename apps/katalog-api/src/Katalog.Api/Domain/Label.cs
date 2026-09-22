namespace Katalog.Api.Domain;

/// <summary>
/// A record label tracked by the user. Labels are app-owned entities - there is no
/// label resource in the Spotify Web API (research §2.2), so "following" a label is
/// modelled entirely in our own database.
/// </summary>
public sealed class Label
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<LabelArtist> LabelArtists { get; } = [];
}
