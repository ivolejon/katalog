using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Katalog.Api.Setup;

public static class SpotifySetup
{
    /// <summary>
    /// Registers the Spotify token provider, the token-owning delegating handler and the typed
    /// catalog client with a custom resilience pipeline (arch report §3.5, fallgropar §6.1):
    /// <c>TotalTimeout → Retry (Retry-After aware) → CircuitBreaker → AttemptTimeout</c>.
    /// Standard defaults are deliberately not used: Spotify 429s can carry a Retry-After longer
    /// than the default total timeout, which would guillotine the retries.
    /// </summary>
    public static IServiceCollection AddSpotify(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SpotifyTokenProvider>();
        services.AddTransient<SpotifyTokenHandler>();

        // Token exchange client (accounts.spotify.com) - no resilience pipeline: the token
        // exchange is cached and serialized by SpotifyTokenProvider so a slow exchange is rare.
        services.AddHttpClient(SpotifyClientNames.Accounts, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SpotifyOptions>>().Value;
            client.BaseAddress = new Uri(options.AccountsBaseUrl);
        });

        services.AddHttpClient<ISpotifyApiClient, SpotifyApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SpotifyOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddHttpMessageHandler<SpotifyTokenHandler>()
        .RedactLoggedHeaders(["Authorization"])
        .AddResilienceHandler("spotify-catalog-resilience", static (pipeline, _) =>
        {
            // Per-attempt timeout is low (a single Spotify catalog call is fast); the total
            // timeout is dimensioned so retries honoured via Retry-After are not guillotined
            // (arch report §6.1: worst case = attempts(4 x 15s) + retry waits).
            var attemptTimeout = TimeSpan.FromSeconds(15);
            var totalTimeout = TimeSpan.FromSeconds(300);

            pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = totalTimeout });

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                UseJitter = true,
                // Honour Spotify's Retry-After header on 429 responses (research §3.6).
                ShouldRetryAfterHeader = true,
                ShouldHandle = static args => ValueTask.FromResult(IsTransientRetry(args)),
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                BreakDuration = TimeSpan.FromSeconds(30),
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                FailureRatio = 0.5,
                ShouldHandle = static args => ValueTask.FromResult(IsTransientBreaker(args)),
            });

            pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = attemptTimeout });
        });

        return services;
    }

    /// <summary>Transient iff 429/5xx/408, transport exceptions or timeouts; user-driven cancellation is not retried.</summary>
    private static bool IsTransientRetry(RetryPredicateArguments<HttpResponseMessage> args)
        => args.Outcome.Exception is OperationCanceledException
            ? !args.Context.CancellationToken.IsCancellationRequested
            : HttpClientResiliencePredicates.IsTransient(args.Outcome);

    /// <summary>Same predicate for the circuit breaker outcome.</summary>
    private static bool IsTransientBreaker(CircuitBreakerPredicateArguments<HttpResponseMessage> args)
        => args.Outcome.Exception is OperationCanceledException
            ? !args.Context.CancellationToken.IsCancellationRequested
            : HttpClientResiliencePredicates.IsTransient(args.Outcome);
}
