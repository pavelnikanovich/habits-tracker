using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace HabitsTracker.Host.Tests;

// The cookie `session` (contracts/http-api.md, "Authentication"; research R4): its attributes, its lifetime, and the
// session it extends.
public class SessionCookieTests(PostgresFixture postgres)
{
    private const string Ann = "ann@example.com";
    private const string Me = "/api/me";

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task A_session_cookie_without_remember_me_has_no_expiry()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync();
        using var client = CreateClient(host);

        using var response = await SignInAsync(host, client, rememberMe: false, ct);

        var header = Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie));
        Assert.Matches("^session=[A-Za-z0-9_-]{43}; path=/; secure; samesite=lax; httponly$", header);

        // Only a Remembered session's cookie is set again by a request that extends the session.
        using var me = await GetAsync(client, Me, TokenOf(header), ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.False(me.Headers.Contains(HeaderNames.SetCookie));
    }

    [Fact]
    public async Task A_remembered_session_cookie_lasts_30_days()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync();
        using var client = CreateClient(host);

        using var response = await SignInAsync(host, client, rememberMe: true, ct);

        var header = Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie));
        Assert.Matches("^session=[A-Za-z0-9_-]{43}; max-age=2592000; path=/; secure; samesite=lax; httponly$", header);

        host.Time.Advance(TimeSpan.FromDays(1));
        using var me = await GetAsync(client, Me, TokenOf(header), ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var renewed = Assert.Single(me.Headers.GetValues(HeaderNames.SetCookie));
        Assert.Equal(header, renewed);
    }

    [Fact]
    public async Task The_cookie_holds_only_the_session_token()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync();
        using var client = CreateClient(host);

        using var response = await SignInAsync(host, client, rememberMe: false, ct);

        var token = TokenOf(Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie)));
        await using var scope = host.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<SessionRecord>().SingleAsync(ct);
        Assert.Equal(SHA256.HashData(Base64Url.DecodeFromChars(token)), stored.Id);
    }

    [Fact]
    public async Task A_request_extends_the_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync();
        using var client = CreateClient(host);
        using var response = await SignInAsync(host, client, rememberMe: false, ct);
        var token = TokenOf(Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie)));

        host.Time.Advance(TimeSpan.FromDays(29));
        using var first = await GetAsync(client, Me, token, ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        host.Time.Advance(TimeSpan.FromDays(29));
        using var second = await GetAsync(client, Me, token, ct);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        host.Time.Advance(TimeSpan.FromDays(30));
        using var idle = await GetAsync(client, Me, token, ct);
        await AssertNotSignedInAsync(idle, ct);
    }

    [Fact]
    public async Task An_unknown_expired_or_malformed_cookie_is_not_signed_in_and_is_cleared()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync();
        using var client = CreateClient(host);
        using var response = await SignInAsync(host, client, rememberMe: true, ct);
        var expired = TokenOf(Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie)));
        host.Time.Advance(TimeSpan.FromDays(30));

        foreach (var (name, cookie) in new[] { ("unknown", SessionToken.Generate().Value), ("expired", expired), ("malformed", "abc") })
        {
            using var me = await GetAsync(client, Me, cookie, ct);
            await AssertNotSignedInAsync(me, ct);

            var cleared = SetCookieHeaderValue.Parse(Assert.Single(me.Headers.GetValues(HeaderNames.SetCookie)));
            Assert.True(cleared.Name == "session", $"{name}: {cleared}");
            Assert.True(cleared.Expires < host.Time.GetUtcNow(), $"{name}: {cleared}");
            Assert.True(cleared.Path == "/" && cleared.Secure && cleared.HttpOnly && cleared.SameSite == SameSiteMode.Lax, $"{name}: {cleared}");
        }
    }

    // Only an endpoint marked signed in reads the session (contracts/http-api.md, "Authentication"): a public endpoint,
    // a file and a page neither extend the session they are sent with nor clear a cookie that names none.
    [Fact]
    public async Task Public_requests_do_not_touch_the_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(stubBuild: true);
        using var client = CreateClient(host);
        using var response = await SignInAsync(host, client, rememberMe: true, ct);
        var token = TokenOf(Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie)));
        host.Time.Advance(TimeSpan.FromDays(29));

        foreach (var cookie in new[] { token, "abc" })
        {
            foreach (var request in PublicRequests(cookie))
            {
                using (request)
                {
                    using var answer = await client.SendAsync(request, ct);
                    Assert.True(answer.StatusCode < HttpStatusCode.InternalServerError, $"{request.Method} {request.RequestUri}: {(int)answer.StatusCode}");
                    Assert.False(answer.Headers.Contains(HeaderNames.SetCookie), $"{request.Method} {request.RequestUri} set a cookie");
                }
            }
        }

        // None of them was activity: the session ends 30 days after the sign-in.
        host.Time.Advance(TimeSpan.FromDays(1));
        using var me = await GetAsync(client, Me, token, ct);
        await AssertNotSignedInAsync(me, ct);
    }

    private static IEnumerable<HttpRequestMessage> PublicRequests(string cookie)
    {
        yield return WithCookie(new HttpRequestMessage(HttpMethod.Get, "/api/locales"), cookie);
        yield return WithCookie(new HttpRequestMessage(HttpMethod.Post, "/api/code-requests")
        {
            Content = JsonContent.Create(new { email = "bob@example.com", locale = "en" }),
        }, cookie);
        yield return WithCookie(new HttpRequestMessage(HttpMethod.Post, "/api/sessions")
        {
            // An email without a code, so the entry never signs in.
            Content = JsonContent.Create(new { email = "carol@example.com", code = "000000", rememberMe = false, timeZone = "UTC", locale = "en" }),
        }, cookie);
        yield return WithCookie(new HttpRequestMessage(HttpMethod.Get, "/" + HostFactory.StubHashedFile), cookie);
        yield return WithCookie(new HttpRequestMessage(HttpMethod.Get, "/privacy"), cookie);
    }

    private async Task<HostFactory> StartAsync(bool stubBuild = false)
    {
        var host = new HostFactory(postgres).WithCapturingSender();
        if (stubBuild)
            host.WithStubBuild();
        host.Time.SetUtcNow(Start);
        await host.InitializeAsync();
        return host;
    }

    private static HttpClient CreateClient(HostFactory host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static async Task<HttpResponseMessage> SignInAsync(HostFactory host, HttpClient client, bool rememberMe, CancellationToken ct)
    {
        using (var requested = await client.PostAsJsonAsync("/api/code-requests", new { email = Ann, locale = "en" }, ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var response = await client.PostAsJsonAsync(
            "/api/sessions", new { email = Ann, code = host.Sender.LastCode(Ann), rememberMe, timeZone = "UTC", locale = "en" }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static string TokenOf(string setCookie) => SetCookieHeaderValue.Parse(setCookie).Value.ToString();

    private static HttpRequestMessage WithCookie(HttpRequestMessage request, string cookie)
    {
        request.Headers.Add(HeaderNames.Cookie, $"session={cookie}");
        return request;
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string cookie, CancellationToken ct) =>
        client.SendAsync(WithCookie(new HttpRequestMessage(HttpMethod.Get, path), cookie), ct);

    private static async Task AssertNotSignedInAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"status {(int)response.StatusCode}; body '{body}'");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("not-signed-in", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
    }
}
