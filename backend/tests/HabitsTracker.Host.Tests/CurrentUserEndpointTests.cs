using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HabitsTracker.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace HabitsTracker.Host.Tests;

// GET /api/me (contracts/http-api.md): signed in; the signed-in user's own data.
public class CurrentUserEndpointTests(PostgresFixture postgres)
{
    private static readonly Uri Me = new("/api/me", UriKind.Relative);

    [Fact]
    public async Task Me_requires_a_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var response = await client.GetAsync(Me, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"status {(int)response.StatusCode}; body '{body}'");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("not-signed-in", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
    }

    // FR-009: a browser that reports no time zone gets UTC, and the page is told it was not recognized.
    [Fact]
    public async Task Me_reports_an_unrecognized_time_zone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using (var requested = await client.PostAsJsonAsync("/api/code-requests", new { email = "ann@example.com", locale = "ru" }, ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        using var signedIn = await client.PostAsJsonAsync("/api/sessions", new
        {
            email = "ann@example.com",
            code = host.Sender.LastCode("ann@example.com"),
            rememberMe = false,
            timeZone = (string?)null,
            locale = "ru",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var cookie = SetCookieHeaderValue.Parse(Assert.Single(signedIn.Headers.GetValues(HeaderNames.SetCookie)));

        using var request = new HttpRequestMessage(HttpMethod.Get, Me);
        request.Headers.Add(HeaderNames.Cookie, $"session={cookie.Value}");
        using var response = await client.SendAsync(request, ct);

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
        await using var host = await StartAsync();
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

    private async Task<HostFactory> StartAsync()
    {
        var host = new HostFactory(postgres).WithCapturingSender();
        await host.InitializeAsync();
        return host;
    }
}
