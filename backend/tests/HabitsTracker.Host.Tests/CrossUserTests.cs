using System.Net;
using System.Net.Http.Json;
using HabitsTracker.Tests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace HabitsTracker.Host.Tests;

// FR-017, SC-003: what user B does through a signed-in endpoint never reaches user A. GET /api/me has its test in
// CurrentUserEndpointTests; the endpoints of the sessions and of the locale have theirs here.
public class CrossUserTests(PostgresFixture postgres)
{
    private static readonly Uri Me = new("/api/me", UriKind.Relative);

    [Fact]
    public async Task B_signing_out_leaves_A_signed_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com");
        using var annRemembered = await host.SignInAsync("ann@example.com", rememberMe: true);
        using var bob = await host.SignInAsync("bob@example.com");

        using var signedOut = await bob.Client.DeleteAsync(new Uri("/api/sessions/current", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        await AssertSignedInAsync(ann, "ann@example.com", ct);
        await AssertSignedInAsync(annRemembered, "ann@example.com", ct);
    }

    [Fact]
    public async Task B_signing_out_everywhere_leaves_A_signed_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com");
        using var annRemembered = await host.SignInAsync("ann@example.com", rememberMe: true);
        using var bob = await host.SignInAsync("bob@example.com");
        using var bobElsewhere = await host.SignInAsync("bob@example.com");

        using var signedOut = await bob.Client.DeleteAsync(new Uri("/api/sessions", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        using var bobsOther = await bobElsewhere.Client.GetAsync(Me, ct);
        await Problem.AssertAsync(bobsOther, HttpStatusCode.Unauthorized, "not-signed-in", ct);
        await AssertSignedInAsync(ann, "ann@example.com", ct);
        await AssertSignedInAsync(annRemembered, "ann@example.com", ct);
    }

    [Fact]
    public async Task B_changing_the_locale_leaves_A_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com", locale: "en");
        using var bob = await host.SignInAsync("bob@example.com", locale: "en");

        using var changed = await bob.Client.PutAsJsonAsync(new Uri("/api/me/locale", UriKind.Relative), new { locale = "pl" }, ct);

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var annsMe = await ann.Client.GetAsync(Me, ct);
        using var bobsMe = await bob.Client.GetAsync(Me, ct);
        Assert.Contains("\"locale\":\"en\"", await annsMe.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        Assert.Contains("\"locale\":\"pl\"", await bobsMe.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    // A signed-in endpoint added without a cross-user test fails here: add its test above, then its name to this list.
    [Fact]
    public async Task Every_signed_in_endpoint_has_a_cross_user_test()
    {
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var client = host.CreateCookielessClient();

        var signedIn = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["DELETE /api/sessions", "DELETE /api/sessions/current", "GET /api/me", "PUT /api/me/locale"],
            signedIn);
    }

    private static async Task AssertSignedInAsync(SignedInClient client, string email, CancellationToken ct)
    {
        using var me = await client.Client.GetAsync(Me, ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains($"\"email\":\"{email}\"", await me.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    private static void Capturing(HostFactory host) => host.WithCapturingSender();
}
