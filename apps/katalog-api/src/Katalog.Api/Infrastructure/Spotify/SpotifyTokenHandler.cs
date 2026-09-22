using System.Net;
using System.Net.Http.Headers;

namespace Katalog.Api.Infrastructure.Spotify;

/// <summary>
/// Token-owning delegating handler: injects the client credentials Bearer token into every
/// catalog call, and on a 401 response performs exactly one forced refresh and retry
/// (arch report §3.5 - Katalog needs token-owning, not the reference's on-behalf-of forwarding).
/// Only content-less requests (GET) are retried; the cloned request carries the same headers.
/// </summary>
public sealed class SpotifyTokenHandler(SpotifyTokenProvider tokenProvider, ILogger<SpotifyTokenHandler>? logger = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // One refresh + retry. Only safe for requests without content (all our catalog calls
        // are GETs; a stream body cannot be replayed).
        if (request.Method != HttpMethod.Get)
            return response;

        response.Dispose();

        var refreshedToken = await tokenProvider.ForceRefreshAsync(cancellationToken);
        using var retryRequest = CloneRequest(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);

        logger?.LogDebug("Spotify responded 401; refreshed token and retrying {Method} {Uri}.", request.Method, request.RequestUri);
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage source)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri);
        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
