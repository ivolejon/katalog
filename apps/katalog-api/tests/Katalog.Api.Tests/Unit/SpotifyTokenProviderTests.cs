using System.Net;
using System.Text;
using Katalog.Api.Infrastructure.Spotify;
using Katalog.Api.Setup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Katalog.Api.Tests.Unit;

public sealed class SpotifyTokenProviderTests
{
    private static SpotifyOptions DefaultOptions => new()
    {
        BaseUrl = "https://placeholder.invalid",
        AccountsBaseUrl = "https://accounts.test.local",
        ClientId = "test-client",
        ClientSecret = "test-secret"
    };

    private static HttpClient StubAccountsClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var inner = new StubHttpMessageHandler(handler);
        return new HttpClient(inner) { BaseAddress = new Uri(DefaultOptions.AccountsBaseUrl) };
    }

    [Fact]
    public async Task GetTokenAsync_FirstCall_FetchesAndCachesToken()
    {
        var requests = 0;
        var timeProvider = new FakeTimeProvider();
        var factory = new StubHttpClientFactory(() => StubAccountsClient(async _ =>
        {
            Interlocked.Increment(ref requests);
            return TokenResponse(3600);
        }));

        var provider = new SpotifyTokenProvider(NullLogger<SpotifyTokenProvider>.Instance,
            Options.Create(DefaultOptions), timeProvider, factory);

        var first = await provider.GetTokenAsync(CancellationToken.None);
        var second = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("access-token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task GetTokenAsync_AfterExpiry_FetchesFreshToken()
    {
        var tokenIndex = 0;
        var timeProvider = new FakeTimeProvider();
        var factory = new StubHttpClientFactory(() => StubAccountsClient(async _ =>
        {
            var index = Interlocked.Increment(ref tokenIndex);
            return TokenResponse(3600, $"access-token-{index}");
        }));

        var provider = new SpotifyTokenProvider(NullLogger<SpotifyTokenProvider>.Instance,
            Options.Create(DefaultOptions), timeProvider, factory);

        var first = await provider.GetTokenAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromHours(1)); // expiry + skew passed
        var second = await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal("access-token-1", first);
        Assert.Equal("access-token-2", second);
        Assert.Equal(2, tokenIndex);
    }

    [Fact]
    public async Task ForceRefreshAsync_InvalidatesCache_AndFetchesNewToken()
    {
        var tokenIndex = 0;
        var timeProvider = new FakeTimeProvider();
        var factory = new StubHttpClientFactory(() => StubAccountsClient(async _ =>
        {
            var index = Interlocked.Increment(ref tokenIndex);
            return TokenResponse(3600, $"access-token-{index}");
        }));

        var provider = new SpotifyTokenProvider(NullLogger<SpotifyTokenProvider>.Instance,
            Options.Create(DefaultOptions), timeProvider, factory);

        var first = await provider.GetTokenAsync(CancellationToken.None);
        var refreshed = await provider.ForceRefreshAsync(CancellationToken.None);

        Assert.Equal("access-token-1", first);
        Assert.Equal("access-token-2", refreshed);
        Assert.Equal(2, tokenIndex);
    }

    [Fact]
    public async Task GetTokenAsync_ConcurrentCallers_OnlyFetchOnce()
    {
        var requests = 0;
        var timeProvider = new FakeTimeProvider();
        var factory = new StubHttpClientFactory(() => StubAccountsClient(async _ =>
        {
            await Task.Delay(50);
            Interlocked.Increment(ref requests);
            return TokenResponse(3600);
        }));

        var provider = new SpotifyTokenProvider(NullLogger<SpotifyTokenProvider>.Instance,
            Options.Create(DefaultOptions), timeProvider, factory);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => provider.GetTokenAsync(CancellationToken.None));
        var tokens = await Task.WhenAll(tasks);

        Assert.All(tokens, t => Assert.Equal("access-token-1", t));
        Assert.Equal(1, requests);
    }

    private static HttpResponseMessage TokenResponse(int expiresIn, string token = "access-token-1")
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"access_token":"{{token}}","token_type":"Bearer","expires_in":{{expiresIn}}}""",
                Encoding.UTF8, "application/json")
        };

    /// <summary>DelegatingHandler that invokes a callback (for token exchange stubbing).</summary>
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }

    private sealed class StubHttpClientFactory(Func<HttpClient> factory) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => factory();
    }
}
