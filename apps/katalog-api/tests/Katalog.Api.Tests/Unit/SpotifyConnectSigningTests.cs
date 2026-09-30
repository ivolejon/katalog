using System.Net;
using Microsoft.AspNetCore.Http;
using Katalog.Api.Api.Endpoints;
using Katalog.Api.Infrastructure.Spotify.User;

namespace Katalog.Api.Tests.Unit;

/// <summary>
/// Unit-level checks for the two pieces of Spotify Connect logic that do not need a database or
/// a Spotify call: the PKCE pair used to protect the sign-in, and the mapping from Spotify's
/// player errors to the problem details the UI renders.
/// </summary>
public sealed class SpotifyConnectSigningTests
{
    [Fact]
    public void CreatePkce_ProducesAUrlSafeVerifierAndItsS256Challenge()
    {
        var (verifier, challenge) = BrowserSession.CreatePkce();

        // RFC 7636: 43-128 characters of unreserved characters for the verifier.
        Assert.InRange(verifier.Length, 43, 128);
        Assert.DoesNotContain('+', verifier);
        Assert.DoesNotContain('/', verifier);
        Assert.DoesNotContain('=', verifier);

        // The challenge is the base64url-encoded SHA-256 of the ASCII verifier (S256).
        var expected = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(expected, challenge);
    }

    [Theory]
    [InlineData("NO_ACTIVE_DEVICE", HttpStatusCode.NotFound, "No active Spotify device.")]
    [InlineData("RESTRICTED_DEVICE", HttpStatusCode.Forbidden, "That Spotify device cannot be controlled.")]
    [InlineData("PREMIUM_REQUIRED", HttpStatusCode.Forbidden, "Spotify Premium is required.")]
    public void PlaybackErrors_MapToTheMessageTheUserNeeds(string reason, HttpStatusCode status, string expectedTitle)
    {
        var problem = ReadProblem(SpotifyConnectErrors.ToProblem(
            new SpotifyPlaybackException(status, reason, "raw Spotify message")));

        Assert.Equal(expectedTitle, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.Equal(reason, problem.Extensions["reason"]!.ToString());
    }

    [Fact]
    public void NotConnected_MapsToASignInProblem()
    {
        var problem = ReadProblem(SpotifyConnectErrors.ToProblem(new SpotifyNotConnectedException()));

        Assert.Equal(401, problem.Status);
        Assert.Equal("Sign in with Spotify first.", problem.Title);
    }

    [Fact]
    public void ExpiredSession_MapsToASignInAgainProblem()
    {
        var problem = ReadProblem(SpotifyConnectErrors.ToProblem(
            new SpotifySessionExpiredException("The Spotify session could not be renewed.")));

        Assert.Equal(401, problem.Status);
        Assert.Equal("Your Spotify session has expired.", problem.Title);
        Assert.Equal("The Spotify session could not be renewed.", problem.Detail);
    }

    [Fact]
    public void AnUnexpectedFailure_IsNotSwallowedIntoAUserFacingProblem()
    {
        // Only the known Spotify situations become problem details; anything else must stay a
        // real error so a bug is not reported to the user as "no device" or "sign in again".
        Assert.Throws<InvalidOperationException>(
            () => SpotifyConnectErrors.ToProblem(new InvalidOperationException("boom")));
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails ReadProblem(IResult result)
    {
        return Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result).ProblemDetails!;
    }
}
