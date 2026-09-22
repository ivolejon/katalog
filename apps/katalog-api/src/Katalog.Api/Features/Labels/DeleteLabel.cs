using Katalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Katalog.Api.Features.Labels;

public sealed class DeleteLabel(KatalogContext context)
{
    public async Task<bool> DeleteAsync(Guid labelId, CancellationToken cancellationToken)
    {
        var label = await context.Labels.FirstOrDefaultAsync(l => l.Id == labelId, cancellationToken);
        if (label is null)
            return false;

        context.Labels.Remove(label);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
