using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Katalog.Api.Infrastructure.Spotify.User;

/// <summary>
/// The MVP has no user accounts, so a browser is identified by an opaque, HttpOnly session
/// cookie issued by the API. It only ever names the row in <c>spotify_user_sessions</c>; the
/// browser never sees a token. The OAuth handshake (state + PKCE verifier) rides in a second,
/// short-lived HttpOnly cookie so the sign-in round trip survives Spotify's redirect back and
/// still lets the callback prove the request is the one this browser started.
/// </summary>
public sealed class BrowserSession(IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
{
    public const string SessionCookieName = "katalog_session";
    public const string OAuthCookieName = "katalog_oauth";

    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan OAuthLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Returns this browser's session id, issuing a new one (and the cookie) when the request
    /// carries no valid session cookie. The id is the primary key of the Spotify sign-in row.
    /// </summary>
    public Guid GetOrCreateSessionId()
    {
        var context = HttpContext;
        if (context is null)
            throw new InvalidOperationException("A browser session is only available inside a request.");

        if (Guid.TryParse(context.Request.Cookies[SessionCookieName], out var existing) && existing != Guid.Empty)
            return existing;

        var created = Guid.CreateVersion7();
        context.Response.Cookies.Append(SessionCookieName, created.ToString(), NewCookieOptions(SessionLifetime));
        return created;
    }

    /// <summary>Stores the CSRF state and the PKCE verifier for the sign-in redirect.</summary>
    public void StoreOAuthHandshake(string state, string codeVerifier)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{state}.{codeVerifier}"));
        var context = RequireContext();
        context.Response.Cookies.Append(OAuthCookieName, payload, NewCookieOptions(OAuthLifetime));
    }

    /// <summary>
    /// Reads and clears the stored handshake, returning null when it is missing (cookie dropped,
    /// expired, or never issued - e.g. a forged callback).
    /// </summary>
    public (string State, string CodeVerifier)? TakeOAuthHandshake()
    {
        var context = HttpContext;
        if (context is null || !context.Request.Cookies.TryGetValue(OAuthCookieName, out var raw) || string.IsNullOrEmpty(raw))
            return null;

        context.Response.Cookies.Delete(OAuthCookieName);

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        }
        catch (FormatException)
        {
            return null;
        }

        var separator = decoded.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0)
            return null;

        return (decoded[..separator], decoded[(separator + 1)..]);
    }

    /// <summary>Generates the PKCE verifier (RFC 7636) and its S256 challenge.</summary>
    public static (string Verifier, string Challenge) CreatePkce()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    /// <summary>Cryptographically random, URL-safe CSRF state value.</summary>
    public static string CreateState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private CookieOptions NewCookieOptions(TimeSpan lifetime)
    {
        var context = RequireContext();
        return new CookieOptions
        {
            HttpOnly = true,
            // Lax (not Strict) so the top-level GET redirect back from Spotify carries the cookie.
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Path = "/",
            Expires = timeProvider.GetUtcNow().UtcDateTime.Add(lifetime),
            IsEssential = true,
        };
    }

    private Microsoft.AspNetCore.Http.HttpContext? HttpContext => httpContextAccessor.HttpContext;

    private Microsoft.AspNetCore.Http.HttpContext RequireContext() =>
        HttpContext ?? throw new InvalidOperationException("A browser session is only available inside a request.");
}
