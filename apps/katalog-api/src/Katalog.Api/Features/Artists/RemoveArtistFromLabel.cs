using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Artists;

public enum RemoveArtistFromLabelStatus
{
    Removed,
    NotFound,
    LastArtistRefused
}

public sealed class RemoveArtistFromLabel(KatalogContext context)
{
    /// <summary>
    /// Removes the artist-label link (artist and albums remain in the database).
    /// Refuses to remove the label's last artist: a followed label must keep at least
    /// one linked artist (polling and the release feed are artist-driven), so deleting
    /// the label is the only way to end a follow.
    /// </summary>
    public async Task<RemoveArtistFromLabelStatus> RemoveAsync(Guid labelId, Guid artistId, CancellationToken cancellationToken)
    {
        var link = await context.LabelArtists.FindAsync([labelId, artistId], cancellationToken);
        if (link is null)
            return RemoveArtistFromLabelStatus.NotFound;

        var linkCount = await context.LabelArtists.CountAsync(la => la.LabelId == labelId, cancellationToken);
        if (linkCount <= 1)
            return RemoveArtistFromLabelStatus.LastArtistRefused;

        context.LabelArtists.Remove(link);
        await context.SaveChangesAsync(cancellationToken);
        return RemoveArtistFromLabelStatus.Removed;
    }
}
