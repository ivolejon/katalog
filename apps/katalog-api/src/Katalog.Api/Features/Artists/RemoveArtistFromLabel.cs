using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Artists;

public sealed class RemoveArtistFromLabel(KatalogContext context)
{
    /// <summary>Removes the artist-label link (artist and albums remain in the database).</summary>
    public async Task<bool> RemoveAsync(Guid labelId, Guid artistId, CancellationToken cancellationToken)
    {
        var link = await context.LabelArtists.FindAsync([labelId, artistId], cancellationToken);
        if (link is null)
            return false;

        context.LabelArtists.Remove(link);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
