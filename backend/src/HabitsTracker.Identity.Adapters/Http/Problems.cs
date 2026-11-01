using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace HabitsTracker.Identity.Adapters.Http;

/// <summary>
/// The error format of contracts/http-api.md: an RFC 9457 problem whose <c>title</c> is the reason phrase and whose
/// <c>type</c> is its default, with the extension members <c>code</c>, <c>retryAt</c> (only when given) and
/// <c>traceId</c>.
/// </summary>
public static class Problems
{
    public const string InvalidRequest = "invalid-request";
    public const string NotFound = "not-found";
    public const string InternalError = "internal-error";

    public static IResult Create(int status, string code, DateTimeOffset? retryAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            // Set, because ASP.NET Core would otherwise fill in a link to the HTTP specification.
            Type = "about:blank",
        };
        problem.Extensions["code"] = code;
        if (retryAt is { } at)
            problem.Extensions["retryAt"] = at.UtcDateTime; // an ISO 8601 UTC instant, "...Z"

        // Here as well as in AddTraceId: a request whose Accept excludes JSON gets the problem written without the
        // problem details service, so without its customization.
        AddTraceId(problem);
        return TypedResults.Problem(problem);
    }

    /// <summary>
    /// For <c>AddProblemDetails</c>: ASP.NET Core puts the whole <c>traceparent</c> in <c>traceId</c>; the contract's
    /// <c>traceId</c> is the W3C trace id alone, the id the request's log lines carry (research R20).
    /// </summary>
    public static void AddTraceId(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        AddTraceId(context.ProblemDetails);
    }

    // The host starts an activity for every request while logging is on, and the code writing a problem runs in it.
    private static void AddTraceId(ProblemDetails problem)
    {
        if (Activity.Current is { } activity)
            problem.Extensions["traceId"] = activity.TraceId.ToHexString();
    }
}
