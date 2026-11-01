using System.Net;
using System.Net.Http.Json;
using HabitsTracker.Tests;

namespace HabitsTracker.Host.Tests;

// PUT /api/me/locale (contracts/http-api.md; US-1.8, FR-023).
public class LocaleEndpointTests(PostgresFixture postgres)
{
    private static readonly Uri Me = new("/api/me", UriKind.Relative);
    private static readonly Uri MyLocale = new("/api/me/locale", UriKind.Relative);

    [Fact]
    public async Task Changing_the_locale_is_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com");

        using var changed = await ann.Client.PutAsJsonAsync(MyLocale, new { locale = "ru" }, ct);

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var me = await ann.Client.GetAsync(Me, ct);
        Assert.Contains("\"locale\":\"ru\"", await me.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unavailable_locale_is_refused_and_changes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com");

        using var changed = await ann.Client.PutAsJsonAsync(MyLocale, new { locale = "de" }, ct);

        await Problem.AssertAsync(changed, HttpStatusCode.BadRequest, "invalid-locale", ct);
        using var me = await ann.Client.GetAsync(Me, ct);
        Assert.Contains("\"locale\":\"en\"", await me.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_locale_is_an_invalid_request()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var ann = await host.SignInAsync("ann@example.com");

        using var changed = await ann.Client.PutAsJsonAsync(MyLocale, new { }, ct);

        await Problem.AssertAsync(changed, HttpStatusCode.BadRequest, "invalid-request", ct);
    }

    [Fact]
    public async Task Changing_the_locale_requires_a_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, Capturing);
        using var client = host.CreateCookielessClient();

        using var changed = await client.PutAsJsonAsync(MyLocale, new { locale = "ru" }, ct);

        await Problem.AssertAsync(changed, HttpStatusCode.Unauthorized, "not-signed-in", ct);
    }

    private static void Capturing(HostFactory host) => host.WithCapturingSender();
}
