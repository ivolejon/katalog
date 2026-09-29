using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Infrastructure.Spotify.User;
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

        AddSpotifyUserConnect(services);

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

    /// <summary>
    /// Spotify Connect: the sign-in round trip and the per-user player calls. These run with the
    /// signed-in user's own token (<see cref="SpotifyUserTokenHandler"/>, one refresh + retry on
    /// 401), kept server-side in the spotify_user_sessions table - never with the app's client
    /// credentials, and never in the browser. Resilience matches the catalog pipeline but with a
    /// shorter total timeout: a player command is a fast call the user is waiting on.
    /// </summary>
    private static void AddSpotifyUserConnect(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<BrowserSession>();
        services.AddScoped<SpotifyUserSessionStore>();
        services.AddScoped<SpotifyUserTokenProvider>();
        services.AddTransient<SpotifyUserTokenHandler>();
        services.AddScoped<ISpotifyUserOAuthService, SpotifyUserOAuthService>();

        // The profile lookup (GET /v1/me) during the callback: the fresh token is set on the
        // request explicitly, so this client must not carry the user token handler.
        services.AddHttpClient(SpotifyClientNames.Profile, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SpotifyOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddHttpClient(SpotifyClientNames.UserPlayback, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SpotifyOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddHttpMessageHandler<SpotifyUserTokenHandler>()
        .RedactLoggedHeaders(["Authorization"])
        .AddResilienceHandler("spotify-user-connect-resilience", static (pipeline, _) =>
        {
            pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(30) });

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = true,
                ShouldRetryAfterHeader = true,
                ShouldHandle = static args => ValueTask.FromResult(IsTransientRetry(args)),
            });

            pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(10) });
        });

        // Scoped, not the transient typed-client default: the client must be built in the
        // request scope so the token handler sees the same scoped session and EF context.
        services.AddScoped<ISpotifyPlaybackClient>(sp =>
            new SpotifyPlaybackClient(sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(SpotifyClientNames.UserPlayback)));
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
