using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace Katalog.Api.Infrastructure.Spotify.User;

/// <summary>
/// Puts the caller's own Spotify Bearer token on every Connect call and, on a 401, renews it
/// once and retries the same request (the app credentials path does the same for catalog calls,
/// see <see cref="SpotifyTokenHandler"/>). The request body is buffered up front so the retry
/// can replay a play request; the bodies involved are tiny JSON documents.
///
/// The provider is resolved per request: the HttpClientFactory caches this handler's pipeline,
/// so a constructor-injected scoped provider (and the EF context under it) would be shared by
/// every request for the handler's lifetime.
/// </summary>
public sealed class SpotifyUserTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<SpotifyUserTokenHandler>? logger = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var tokenProvider = RequireTokenProvider();
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        var renewed = await tokenProvider.ForceRefreshAsync(cancellationToken);
        using var retry = CloneRequest(request, body);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", renewed);

        logger?.LogDebug("Spotify answered 401; renewed the user's token and retried {Method}.", request.Method);
        return await base.SendAsync(retry, cancellationToken);
    }

    private SpotifyUserTokenProvider RequireTokenProvider()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
            throw new InvalidOperationException("The Spotify user token handler only runs inside a request.");
        return context.RequestServices.GetRequiredService<SpotifyUserTokenProvider>();
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage source, byte[]? body)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri);
        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in source.Content!.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
