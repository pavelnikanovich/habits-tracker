using HabitsTracker.Identity.Core;
using Microsoft.AspNetCore.Http;

namespace HabitsTracker.Identity.Adapters.Http;

/// <summary>
/// The cookie <c>session</c> (contracts/http-api.md, "Authentication"; research R4): only the session token,
/// <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Lax</c>, <c>Path=/</c>, no <c>Domain</c>; no <c>Expires</c> or
/// <c>Max-Age</c> for a session that is not a Remembered session, <c>Max-Age=2592000</c> (30 days) for a Remembered one.
/// Written here rather than by ASP.NET Core's cookie authentication, so these are its only attributes.
/// </summary>
public static class SessionCookie
{
    public const string Name = "session";

    private const int RememberedMaxAgeSeconds = 2592000;

    public static void Append(HttpResponse response, SessionToken token, bool remembered)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(token);

        var options = Options();
        if (remembered)
            options.MaxAge = TimeSpan.FromSeconds(RememberedMaxAgeSeconds);
        response.Cookies.Append(Name, token.Value, options);
    }

    /// <summary>Tells the browser to drop the cookie: the same attributes, with an expiry in the past.</summary>
    public static void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(Name, Options());
    }

    /// <summary>The token the request's cookie carries, or null when there is no cookie or its value is not a session token.</summary>
    public static SessionToken? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Cookies.TryGetValue(Name, out var value))
            return null;

        try
        {
            return SessionToken.Create(value);
        }
        catch (DomainException exception) when (exception.Error == DomainError.InvalidSessionToken)
        {
            return null;
        }
    }

    // Secure in every environment, development included: Chromium accepts a Secure cookie from http://localhost (research R4).
    private static CookieOptions Options() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
    };
}
