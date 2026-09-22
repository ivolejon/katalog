using Katalog.Api.Infrastructure;
using Katalog.Api.Setup;
using Microsoft.Extensions.Options;

namespace Katalog.Api.Features.Releases.Polling;

/// <summary>
/// Hosted service that drives the release poll on a configurable rhythm (6-24 h, default 12 h).
/// Polls once directly at start, then on every PeriodicTimer tick (arch report §3.7; reference
/// pattern "poll once up front - PeriodicTimer waits a full interval before its first tick").
/// </summary>
public sealed class ReleasesPollingService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReleasesPollingService> logger,
    IOptionsMonitor<PollingOptions> pollingOptions) : MigrationAwareBackgroundService<KatalogContext>(scopeFactory, logger)
{
    protected override async Task ExecuteCoreAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(pollingOptions.CurrentValue.Interval);

        await PollOnceAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollOnceAsync(stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = ScopeFactory.CreateScope();
            var poller = scope.ServiceProvider.GetRequiredService<ReleasePoller>();
            await poller.PollOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failing cycle must not kill the host; the next interval retries. Unexpected
            // exceptions still escape up and StopHost makes them visible as a restart.
            logger.LogError(ex, "Release polling cycle failed.");
        }
    }
}
