using Microsoft.AspNetCore.Builder;
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

        return endpoints;
    }
}
