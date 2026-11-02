using System.Net;
using HabitsTracker.Identity.Core;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SignInResult = HabitsTracker.Identity.Core.SignInResult;

namespace HabitsTracker.Identity.Adapters.Http;

/// <summary>The endpoints of contracts/http-api.md.</summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // The available locales, read from the translation files (US-1.8, FR-020). The list arrives as a function
        // registered by IdentityContextRegistration, so this adapter names no type of the translation adapter.
        endpoints.MapGet("/api/locales", ([FromServices] Func<IReadOnlyList<LocaleResponse>> listLocales) => listLocales());

        endpoints.MapPost("/api/code-requests", RequestCodeAsync);
        endpoints.MapPost("/api/sessions", SignInAsync);
        endpoints.MapGet("/api/me", GetCurrentUserAsync).RequireAuthorization();
        endpoints.MapPut("/api/me/locale", ChangeLocaleAsync).RequireAuthorization();
        endpoints.MapDelete("/api/sessions/current", SignOutAsync).RequireAuthorization();
        endpoints.MapDelete("/api/sessions", SignOutEverywhereAsync).RequireAuthorization();

        return endpoints;
    }

    // US-1.1. Public. The answer is the same for an email with a user and one without (FR-003): only the use case
    // knows about codes, and it never reads users.
    private static async Task<IResult> RequestCodeAsync(
        CodeRequestBody body, HttpContext context, [FromServices] RequestCode requestCode)
    {
        var email = Email.Create(body.Email);
        // The address the forwarded-headers middleware settled on: the proxy's X-Forwarded-For when it came from the
        // proxy network, else the connection's (research R7).
        var address = context.Connection.RemoteIpAddress
            ?? throw new InvalidOperationException("The connection has no remote IP address.");

        // Not the request's abort token (ruling R-105): a client that closes the connection while the email is on its
        // way would roll back the counted request after the email left, and a script that disconnects mid-send would get
        // unlimited code emails (FR-004; R19, D1, D2). The sender's 10-second timeout and the lock wait bound the work.
        var result = await requestCode.ExecuteAsync(email, body.Locale, IpAddressPrefix.From(address), CancellationToken.None);

        return result switch
        {
            CodeRequestResult.Sent sent => TypedResults.Json(new CodeRequestAccepted(sent.Remaining, sent.Limit), statusCode: StatusCodes.Status202Accepted),
            CodeRequestResult.RefusedByEmailLimit refused => Problems.Create(StatusCodes.Status429TooManyRequests, ProblemCodes.EmailLimit, refused.RetryAt),
            CodeRequestResult.RefusedByIpLimit refused => Problems.Create(StatusCodes.Status429TooManyRequests, ProblemCodes.IpLimit, refused.RetryAt),
            CodeRequestResult.RefusedByEmailService { Reason: EmailServiceRefusal.Unavailable } =>
                Problems.Create(StatusCodes.Status503ServiceUnavailable, ProblemCodes.EmailServiceUnavailable, retryAt: null),
            CodeRequestResult.RefusedByEmailService refused =>
                Problems.Create(StatusCodes.Status503ServiceUnavailable, ProblemCodes.EmailServiceLimit, refused.RetryAt),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    // US-1.2. Public: a request with no session signs in, and one with a session signs in anew. The session the
    // browser held is passed to the use case, which revokes it; its token is never reused (research R19, S4).
    private static async Task<IResult> SignInAsync(
        SignInBody body, HttpContext context, [FromServices] SignIn signIn)
    {
        var email = Email.Create(body.Email);
        var code = OneTimeCodeValue.Create(body.Code);

        var result = await signIn.ExecuteAsync(
            email, code, body.RememberMe, body.TimeZone, body.Locale, SessionCookie.Read(context.Request), context.RequestAborted);

        switch (result)
        {
            case SignInResult.SignedIn signedIn:
                SessionCookie.Append(context.Response, signedIn.Token, signedIn.Session.Remembered);
                return TypedResults.Json(CurrentUserResponse.Of(signedIn.User));
            case SignInResult.CodeIncorrect:
                return Problems.Create(StatusCodes.Status401Unauthorized, ProblemCodes.CodeIncorrect, retryAt: null);
            case SignInResult.NewCodeRequired:
                return Problems.Create(StatusCodes.Status401Unauthorized, ProblemCodes.NewCodeRequired, retryAt: null);
            default:
                throw new System.Diagnostics.UnreachableException();
        }
    }

    // US-1.2, US-1.6. Signed in: the user of the session, never another (FR-017).
    private static async Task<CurrentUserResponse> GetCurrentUserAsync(
        HttpContext context, [FromServices] GetCurrentUser getCurrentUser) =>
        CurrentUserResponse.Of(await getCurrentUser.ExecuteAsync(SessionAuthenticationHandler.UserIdOf(context.User), context.RequestAborted));

    // US-1.8, FR-023. Signed in: the locale of the session's user, never another's (FR-017).
    private static async Task<IResult> ChangeLocaleAsync(
        LocaleBody body, HttpContext context, [FromServices] ChangeLocale changeLocale)
    {
        await changeLocale.ExecuteAsync(SessionAuthenticationHandler.UserIdOf(context.User), body.Locale, context.RequestAborted);
        return TypedResults.NoContent();
    }

    // US-1.4, FR-015. Signed in: revokes the session of this request only. The cookie is cleared even when the session
    // was Remembered and the authentication handler has just set it again.
    private static async Task<IResult> SignOutAsync(HttpContext context, [FromServices] SignOut signOut)
    {
        await signOut.ExecuteAsync(SessionAuthenticationHandler.SessionIdOf(context.User), context.RequestAborted);
        SessionCookie.Clear(context.Response);
        return TypedResults.NoContent();
    }

    // US-1.5, FR-015. Signed in: revokes every session of the request's user, this one included.
    private static async Task<IResult> SignOutEverywhereAsync(HttpContext context, [FromServices] SignOutEverywhere signOutEverywhere)
    {
        await signOutEverywhere.ExecuteAsync(SessionAuthenticationHandler.UserIdOf(context.User), context.RequestAborted);
        SessionCookie.Clear(context.Response);
        return TypedResults.NoContent();
    }

    // A missing field does not bind: invalid-request. A field of another shape is rejected by the core: invalid-email, invalid-locale.
    private sealed record CodeRequestBody
    {
        public required string Email { get; init; }

        public required string Locale { get; init; }
    }

    private sealed record LocaleBody
    {
        public required string Locale { get; init; }
    }

    private sealed record CodeRequestAccepted(int Remaining, int Limit);

    // Every field is required, timeZone too, though it may be null (the browser reports none); a missing one does not bind.
    private sealed record SignInBody
    {
        public required string Email { get; init; }

        public required string Code { get; init; }

        public required bool RememberMe { get; init; }

        public required string? TimeZone { get; init; }

        public required string Locale { get; init; }
    }

    // The current user, as POST /api/sessions and GET /api/me answer it.
    private sealed record CurrentUserResponse(string Email, string TimeZone, bool TimeZoneRecognized, string Locale)
    {
        public static CurrentUserResponse Of(User user) =>
            new(user.Email.Value, user.TimeZone.Id, user.TimeZone.Recognized, user.Locale.Code);
    }
}
