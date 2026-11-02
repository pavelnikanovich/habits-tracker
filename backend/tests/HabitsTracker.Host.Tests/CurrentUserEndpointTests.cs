using System.Net;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// GET /api/me (contracts/http-api.md): signed in; the signed-in user's own data.
public class CurrentUserEndpointTests(PostgresFixture postgres)
{
    private static readonly Uri Me = new("/api/me", UriKind.Relative);

    [Fact]
    public async Task Me_requires_a_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var client = host.CreateCookielessClient();

        using var response = await client.GetAsync(Me, ct);

        await Problem.AssertAsync(response, HttpStatusCode.Unauthorized, "not-signed-in", ct);
    }

    // FR-009: a browser that reports no time zone gets UTC, and the page is told it was not recognized.
    [Fact]
    public async Task Me_reports_an_unrecognized_time_zone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com", timeZone: null, locale: "ru");

        using var response = await ann.Client.GetAsync(Me, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"email":"ann@example.com","timeZone":"UTC","timeZoneRecognized":false,"locale":"ru"}""",
            await response.Content.ReadAsStringAsync(ct));
    }

    // FR-017, the cross-user test: with user B's cookie the answer is B's data, never A's.
    [Fact]
    public async Task Me_of_user_B_is_never_user_A()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com");
        using var bob = await host.SignInAsync("bob@example.com");

        using var bobsMe = await bob.Client.GetAsync(Me, ct);
        using var annsMe = await ann.Client.GetAsync(Me, ct);

        Assert.Equal(
            """{"email":"bob@example.com","timeZone":"UTC","timeZoneRecognized":true,"locale":"en"}""",
            await bobsMe.Content.ReadAsStringAsync(ct));
        Assert.Equal(
            """{"email":"ann@example.com","timeZone":"UTC","timeZoneRecognized":true,"locale":"en"}""",
            await annsMe.Content.ReadAsStringAsync(ct));
    }

    private static void Capturing(HostFactory host) => host.WithCapturingSender();
}
