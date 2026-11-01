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

        return $"{context.Request.Method} {route.RoutePattern.RawText}";
    }
}

/// <summary>Marks the endpoint that serves the application's pages, which is recorded as <c>static</c>.</summary>
public sealed class ApplicationFallbackMetadata;

public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestTimingMiddleware>();
}
