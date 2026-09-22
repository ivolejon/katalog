using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Katalog.Api.Features.Releases.Polling;

/// <summary>
/// Base class for background services that must wait for unapplied EF Core migrations before
/// starting (arch report §3.7, reference MigrationAwareBackgroundService). The wait happens inside
/// ExecuteAsync, so host startup (HTTP serving, health endpoints) is never blocked by a slow
/// migration or an unreachable database - the worker idles until the schema is ready.
/// </summary>
/// <typeparam name="TDbContext">The application's DbContext used to detect unapplied migrations.</typeparam>
public abstract class MigrationAwareBackgroundService<TDbContext>(IServiceScopeFactory scopeFactory, ILogger logger)
    : BackgroundService
    where TDbContext : DbContext
{
    private static readonly TimeSpan MigrationCheckInterval = TimeSpan.FromSeconds(15);

    protected IServiceScopeFactory ScopeFactory { get; } = scopeFactory;

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForMigrationsAsync(stoppingToken);

        logger.LogInformation("All migrations applied, starting {ServiceName}.", GetType().Name);

        await ExecuteCoreAsync(stoppingToken);
    }

    /// <summary>The worker's actual loop. Invoked once all migrations are applied.</summary>
    protected abstract Task ExecuteCoreAsync(CancellationToken stoppingToken);

    private async Task WaitForMigrationsAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var scope = ScopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                if (!(await dataContext.Database.GetPendingMigrationsAsync(ct)).Any())
                    return;

                logger.LogInformation(
                    "Pending migrations detected, waiting for {Seconds} seconds before checking again.",
                    MigrationCheckInterval.TotalSeconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not check for pending migrations for {ServiceName}, retrying in {Seconds} seconds.",
                    GetType().Name, MigrationCheckInterval.TotalSeconds);
            }

            await Task.Delay(MigrationCheckInterval, ct);
        }
    }
}
