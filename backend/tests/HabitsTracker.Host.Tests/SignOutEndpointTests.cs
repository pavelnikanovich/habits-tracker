using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace HabitsTracker.Host.Tests;

// DELETE /api/sessions/current and DELETE /api/sessions (contracts/http-api.md; FR-015).
public class SignOutEndpointTests(PostgresFixture postgres)
{
    private const string Ann = "ann@example.com";
    private static readonly Uri Me = new("/api/me", UriKind.Relative);
    private static readonly Uri Current = new("/api/sessions/current", UriKind.Relative);
    private static readonly Uri Sessions = new("/api/sessions", UriKind.Relative);

    [Fact]
    public async Task Sign_out_revokes_only_the_current_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var first = await host.SignInAsync(Ann);
        using var second = await host.SignInAsync(Ann);

        using var signedOut = await first.Client.DeleteAsync(Current, ct);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        var cleared = Assert.Single(signedOut.Headers.GetValues(HeaderNames.SetCookie));
        Assert.Matches("^session=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; secure; samesite=lax; httponly$", cleared);
        // The row is gone, not only the cookie: the other session's row stays.
        Assert.Equal([IdOf(second.Token)], await StoredSessionIdsAsync(host, ct));
        using var firstMe = await first.Client.GetAsync(Me, ct);
        await Problem.AssertAsync(firstMe, HttpStatusCode.Unauthorized, "not-signed-in", ct);
        using var secondMe = await second.Client.GetAsync(Me, ct);
        Assert.Equal(HttpStatusCode.OK, secondMe.StatusCode);
    }

    // The renewal of a Remembered session's cookie runs before the endpoint: the answer still leaves the browser with
    // exactly one session Set-Cookie, the clearing one.
    [Theory]
    [InlineData("/api/sessions/current")]
    [InlineData("/api/sessions")]
    public async Task Signing_out_a_remembered_session_leaves_only_the_clearing_cookie(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync(Ann, rememberMe: true);

        using var signedOut = await ann.Client.DeleteAsync(new Uri(path, UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        var cookie = Assert.Single(signedOut.Headers.GetValues(HeaderNames.SetCookie));
        Assert.Matches("^session=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; secure; samesite=lax; httponly$", cookie);
        Assert.Empty(await StoredSessionIdsAsync(host, ct));
    }

    [Fact]
    public async Task Sign_out_everywhere_revokes_every_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var first = await host.SignInAsync(Ann);
        using var second = await host.SignInAsync(Ann, rememberMe: true);

        using var signedOut = await first.Client.DeleteAsync(Sessions, ct);

        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Single(signedOut.Headers.GetValues(HeaderNames.SetCookie));
        Assert.Empty(await StoredSessionIdsAsync(host, ct));
        using var firstMe = await first.Client.GetAsync(Me, ct);
        await Problem.AssertAsync(firstMe, HttpStatusCode.Unauthorized, "not-signed-in", ct);
        using var secondMe = await second.Client.GetAsync(Me, ct);
        await Problem.AssertAsync(secondMe, HttpStatusCode.Unauthorized, "not-signed-in", ct);
    }

    [Theory]
    [InlineData("DELETE", "/api/sessions/current")]
    [InlineData("DELETE", "/api/sessions")]
    public async Task Signing_out_requires_a_session(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var client = host.CreateCookielessClient();

        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative)), ct);

        await Problem.AssertAsync(response, HttpStatusCode.Unauthorized, "not-signed-in", ct);
    }

    // The ids stored are the SHA-256 of the token's bytes.
    private static byte[] IdOf(string token) => SHA256.HashData(Base64Url.DecodeFromChars(token));

    private static async Task<List<byte[]>> StoredSessionIdsAsync(HostFactory host, CancellationToken ct)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<SessionRecord>()
            .Select(record => record.Id).ToListAsync(ct);
    }

    private static void Capturing(HostFactory host) => host.WithCapturingSender();
}
