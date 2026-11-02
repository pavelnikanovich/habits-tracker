using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HabitsTracker.Identity.Adapters.Http;

/// <summary>
/// The scheme <c>session</c> (research R4): the session the cookie names, through <see cref="AuthenticateSession"/>,
/// which extends it (FR-013). It runs only for an endpoint marked signed in: it is not the default scheme, so a public
/// endpoint, a file or a page never reads the session. A missing, malformed, unknown or expired cookie is not signed
/// in; the challenge answers <c>401</c> <c>not-signed-in</c> and clears the cookie.
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthenticateSession authenticateSession)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "session";

    /// <summary>The claim of the user id.</summary>
    public const string UserIdClaim = "sub";

    /// <summary>The claim of the session id, base64url.</summary>
    public const string SessionIdClaim = "sid";

    /// <summary>The user id an endpoint marked signed in acts for.</summary>
    public static Guid UserIdOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Guid.Parse(user.FindFirstValue(UserIdClaim)
            ?? throw new InvalidOperationException("The request is not signed in: the endpoint is not marked signed in."));
    }

    /// <summary>The id of the session an endpoint marked signed in acts for: the bytes the <c>sid</c> claim encodes.</summary>
    public static byte[] SessionIdOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Base64Url.DecodeFromChars(user.FindFirstValue(SessionIdClaim)
            ?? throw new InvalidOperationException("The request is not signed in: the endpoint is not marked signed in."));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // The failure messages name no value: the authentication handler logs them.
        var token = SessionCookie.Read(Request);
        if (token is null)
            return AuthenticateResult.Fail("No session cookie, or one that is not a session token.");

        var authenticated = await authenticateSession.ExecuteAsync(token, Context.RequestAborted);
        if (authenticated is null)
            return AuthenticateResult.Fail("The session is unknown or expired.");

        // FR-014: every response that extends a Remembered session gives its cookie 30 days again.
        if (authenticated.Session.Remembered)
            SessionCookie.Append(Response, token, remembered: true);

        var identity = new ClaimsIdentity(
            [
                new Claim(UserIdClaim, authenticated.User.Id.ToString()),
                new Claim(SessionIdClaim, Base64Url.EncodeToString(authenticated.Session.Id)),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        SessionCookie.Clear(Response);
        await Problems.Create(StatusCodes.Status401Unauthorized, ProblemCodes.NotSignedIn, retryAt: null).ExecuteAsync(Context);
    }
}
