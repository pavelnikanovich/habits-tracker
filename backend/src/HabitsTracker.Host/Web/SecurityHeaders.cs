namespace HabitsTracker.Host.Web;

/// <summary>
/// The security headers of every response (research R11, R19 T2 and T3; FR-027): scripts, styles, images and
/// connections only from the origin, no framing, no MIME sniffing, no referrer. Styles allow <c>'unsafe-inline'</c>
/// for the component styles Angular injects; it allows no other server.
/// </summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            // Set as the response starts, so an error response that clears the headers before it still gets them.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            });
            return next(context);
        });
}
