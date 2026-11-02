using HabitsTracker.Identity.Adapters.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Net.Http.Headers;

namespace HabitsTracker.Host.Web;

/// <summary>
/// The rules every request under <c>/api</c> shares (contracts/http-api.md, the shared error table): a body of at most
/// 16 KB (research R19, D4) and only <c>application/json</c> (research R19, T1), both else <c>invalid-request</c>; and
/// an error answered without a body (no endpoint for the path, another method, a body that does not bind) becomes a
/// problem with its code.
/// </summary>
public static class ApiRequestRules
{
    public const int MaxBodyBytes = 16 * 1024;

    public static IApplicationBuilder UseApiRequestRules(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // Routing ignores the case of a path, so /API/locales is under the rules too.
            if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            if (await RefusalOfBodyAsync(context) is { } refusal)
            {
                await Problems.Create(refusal, ProblemCodes.InvalidRequest, retryAt: null).ExecuteAsync(context);
                return;
            }

            await next(context);

            var response = context.Response;
            if (response.HasStarted || response.StatusCode < 400 || response.ContentLength is not null || !string.IsNullOrEmpty(response.ContentType))
                return;

            var status = response.StatusCode;
            if (status == StatusCodes.Status405MethodNotAllowed && !AnApiEndpointServes(context))
            {
                status = StatusCodes.Status404NotFound;
                response.Headers.Remove(HeaderNames.Allow);
            }

            await Problems.Create(status, CodeOf(status), retryAt: null).ExecuteAsync(context);
        });

    private static string CodeOf(int status) => status switch
    {
        StatusCodes.Status404NotFound => ProblemCodes.NotFound,
        >= 500 => ProblemCodes.InternalError,
        _ => ProblemCodes.InvalidRequest,
    };

    // Routing counts the application's fallback route (GET and HEAD, any path) as a candidate before its constraint
    // keeps it out of /api, so it answers 405 to another method on any path; the 405 stands only where an endpoint of
    // the API serves the path.
    private static bool AnApiEndpointServes(HttpContext context)
    {
        var path = context.Request.Path;
        return context.RequestServices.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => IsUnderApi(endpoint.RoutePattern))
            .Any(endpoint => new TemplateMatcher(new RouteTemplate(endpoint.RoutePattern), new RouteValueDictionary(endpoint.RoutePattern.Defaults))
                .TryMatch(path, new RouteValueDictionary()));
    }

    private static bool IsUnderApi(RoutePattern pattern) =>
        pattern.PathSegments is [{ Parts: [RoutePatternLiteralPart literal] }, ..]
        && literal.Content.Equals("api", StringComparison.OrdinalIgnoreCase);

    private static async Task<int?> RefusalOfBodyAsync(HttpContext context)
    {
        var request = context.Request;
        var hasBody = request.ContentLength is { } length
            ? length > 0
            : context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true;
        if (!hasBody)
            return null;

        if (request.ContentLength > MaxBodyBytes)
            return StatusCodes.Status413PayloadTooLarge;

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !contentType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status415UnsupportedMediaType;

        if (request.ContentLength is null && !await BufferWithinLimitAsync(context))
            return StatusCodes.Status413PayloadTooLarge;

        return null;
    }

    // A body sent without Content-Length (chunked) is read here, up to the limit, so the limit holds whatever the server.
    private static async Task<bool> BufferWithinLimitAsync(HttpContext context)
    {
        var buffer = new MemoryStream();
        context.Response.RegisterForDispose(buffer);
        var chunk = new byte[4096];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
                return false;
            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        context.Request.Body = buffer;
        context.Request.ContentLength = buffer.Length;
        return true;
    }
}
