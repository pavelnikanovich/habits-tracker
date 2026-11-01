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

        return endpoints;
    }

    // US-1.1. Public. The answer is the same for an email with a user and one without (FR-003): only the use case
    // knows about codes, and it never reads users.
    private static async Task<IResult> RequestCodeAsync(
        CodeRequestBody body, HttpContext context, [FromServices] RequestCode requestCode, [FromServices] ILocales locales)
    {
        var email = Email.Create(body.Email);
        var locale = Locale.Create(body.Locale, locales);
        // The address the forwarded-headers middleware settled on: the proxy's X-Forwarded-For when it came from the
        // proxy network, else the connection's (research R7).
        var address = context.Connection.RemoteIpAddress
            ?? throw new InvalidOperationException("The connection has no remote IP address.");

        var result = await requestCode.ExecuteAsync(email, locale, IpAddressPrefix.From(address), context.RequestAborted);

        return result switch
        {
            CodeRequestResult.Sent sent => TypedResults.Json(new CodeRequestAccepted(sent.Remaining, sent.Limit), statusCode: StatusCodes.Status202Accepted),
            CodeRequestResult.RefusedByEmailLimit refused => Problems.Create(StatusCodes.Status429TooManyRequests, "email-limit", refused.RetryAt),
            CodeRequestResult.RefusedByIpLimit refused => Problems.Create(StatusCodes.Status429TooManyRequests, "ip-limit", refused.RetryAt),
            CodeRequestResult.RefusedByEmailService { Reason: EmailServiceRefusal.Unavailable } =>
                Problems.Create(StatusCodes.Status503ServiceUnavailable, "email-service-unavailable", retryAt: null),
            CodeRequestResult.RefusedByEmailService refused =>
                Problems.Create(StatusCodes.Status503ServiceUnavailable, "email-service-limit", refused.RetryAt),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
    }

    // US-1.2. Public: a request with no session signs in, and one with a session signs in anew. The session the
    // browser held is passed to the use case, which revokes it; its token is never reused (research R19, S4).
    private static async Task<IResult> SignInAsync(
        SignInBody body, HttpContext context, [FromServices] SignIn signIn, [FromServices] ILocales locales)
    {
        var email = Email.Create(body.Email);
        var code = OneTimeCodeValue.Create(body.Code);
        var locale = Locale.Create(body.Locale, locales);

        var result = await signIn.ExecuteAsync(
            email, code, body.RememberMe, body.TimeZone, locale, SessionCookie.Read(context.Request), context.RequestAborted);

        switch (result)
        {
            case SignInResult.SignedIn signedIn:
                SessionCookie.Append(context.Response, signedIn.Token, signedIn.Session.Remembered);
                return TypedResults.Json(CurrentUserResponse.Of(signedIn.User));
            case SignInResult.CodeIncorrect:
                return Problems.Create(StatusCodes.Status401Unauthorized, "code-incorrect", retryAt: null);
            case SignInResult.NewCodeRequired:
                return Problems.Create(StatusCodes.Status401Unauthorized, "new-code-required", retryAt: null);
            default:
                throw new System.Diagnostics.UnreachableException();
        }
    }

    // US-1.2, US-1.6. Signed in: the user of the session, never another (FR-017).
    private static async Task<CurrentUserResponse> GetCurrentUserAsync(
        HttpContext context, [FromServices] GetCurrentUser getCurrentUser) =>
        CurrentUserResponse.Of(await getCurrentUser.ExecuteAsync(SessionAuthenticationHandler.UserIdOf(context.User), context.RequestAborted));

    // A missing field does not bind: invalid-request. A field of another shape is rejected by the core: invalid-email, invalid-locale.
    private sealed record CodeRequestBody
    {
        public required string Email { get; init; }

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
