using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace HabitsTracker.Host.Metrics;

/// <summary>
/// Times each request into <see cref="RequestTimingHistogram"/> (FR-039, research R9). First in the pipeline, so it
/// covers every other middleware. It measures with <see cref="TimeProvider"/>, so tests drive the durations.
/// </summary>
public sealed class RequestTimingMiddleware(
    RequestDelegate next, TimeProvider time, RequestTimingHistogram histogram)
{
    /// <summary>The endpoint name of files and of the application fallback.</summary>
    public const string StaticEndpoint = "static";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = time.GetTimestamp();
        var hour = Truncate(time.GetUtcNow());
        try
        {
            await next(context);
        }
        finally
        {
            histogram.Record(hour, EndpointOf(context), time.GetElapsedTime(started));
        }
    }

    private static DateTimeOffset Truncate(DateTimeOffset moment) =>
        new(moment.UtcDateTime.Ticks - (moment.UtcDateTime.Ticks % TimeSpan.TicksPerHour), TimeSpan.Zero);

    // The route pattern, never the path: a pattern holds no value of the request (FR-039). The exception handler clears
    // the endpoint of a request that threw, and keeps it in its feature. Files and unmatched paths have none; the
    // application fallback is marked with ApplicationFallbackMetadata.
    private static string EndpointOf(HttpContext context)
    {
        var endpoint = context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
        if (endpoint is not RouteEndpoint route || endpoint.Metadata.GetMetadata<ApplicationFallbackMetadata>() is not null)
            return StaticEndpoint;

        return $"{MethodOf(context.Request.Method)} {route.RoutePattern.RawText}";
    }

    // The client chooses the method, and an endpoint such as /health accepts any: only a fixed set is recorded, so the
    // endpoint column stays bounded and holds nothing a client wrote (FR-039).
    private static string MethodOf(string method) =>
        KnownMethods.Contains(method) ? method : "OTHER";

    private static readonly HashSet<string> KnownMethods =
        new(["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"], StringComparer.Ordinal);
}

/// <summary>Marks the endpoint that serves the application's pages, which is recorded as <c>static</c>.</summary>
public sealed class ApplicationFallbackMetadata;

public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestTimingMiddleware>();
}
