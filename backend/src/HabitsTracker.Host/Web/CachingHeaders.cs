using System.Text.RegularExpressions;

namespace HabitsTracker.Host.Web;

/// <summary>
/// HTTP caching (research R21): the application and the translation files are checked with the server each time, so a
/// deployment reaches the browser at once; build files with a content hash in their name are kept for a year; no
/// answer under <c>/api</c> or <c>/health</c> is stored (research R19, I6).
/// </summary>
public static partial class CachingHeaders
{
    private const string NoStore = "no-store";
    private const string NoCache = "no-cache";
    private const string Immutable = "public, max-age=31536000, immutable";

    public static IApplicationBuilder UseCachingHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = RuleFor(context);
                return Task.CompletedTask;
            });
            return next(context);
        });

    private static string RuleFor(HttpContext context)
    {
        var path = context.Request.Path;
        // Routing ignores the case of a path, so /API/locales reaches the API and must not be stored either.
        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
            return NoStore;

        var found = context.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status304NotModified;
        return found && HashedFileName().IsMatch(path.Value ?? "") ? Immutable : NoCache;
    }

    // Angular's production build (outputHashing "all") names its files <name>-<HASH>.<extension>, e.g. main-ABC123.js.
    // Linear time without a timeout, which would count elapsed time and fail under load (RegexEngineTests).
    [GeneratedRegex("/[^/]+-[A-Z0-9]{6,}\\.[a-z0-9]+$", RegexOptions.NonBacktracking)]
    private static partial Regex HashedFileName();
}
