using System.Net;
using HabitsTracker.Identity.Core;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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

    // A missing field does not bind: invalid-request. A field of another shape is rejected by the core: invalid-email, invalid-locale.
    private sealed record CodeRequestBody
    {
        public required string Email { get; init; }

        public required string Locale { get; init; }
    }

    private sealed record CodeRequestAccepted(int Remaining, int Limit);
}
