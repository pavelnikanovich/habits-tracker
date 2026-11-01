using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeZone = HabitsTracker.Identity.Core.TimeZone;

namespace HabitsTracker.Host.Tests;

// POST /api/code-requests (contracts/http-api.md), over PostgreSQL, with the default limits and a capturing sender.
public class CodeRequestEndpointTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-02T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Sends_a_code_in_the_page_language_and_reports_the_remaining_count()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();

        using var response = await PostAsync(client, "ann@example.com", "pl", ct: ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("""{"remaining":4,"limit":5}""", await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("pl", host.Sender.LastLocale("ann@example.com"));
        Assert.Matches("^[0-9]{6}$", host.Sender.LastCode("ann@example.com"));
    }

    [Fact]
    public async Task Default_limits_are_five_per_email_and_ten_per_ip()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();

        foreach (var remaining in new[] { 4, 3, 2, 1, 0 })
        {
            using var accepted = await PostAsync(client, "ann@example.com", ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            Assert.Equal(remaining, (await ReadAsync(accepted, ct)).GetProperty("remaining").GetInt32());
        }

        using var emailLimit = await PostAsync(client, "ann@example.com", ct: ct);
        var problem = await Problem.AssertAsync(emailLimit, HttpStatusCode.TooManyRequests, "email-limit", ct);
        Assert.Equal("2026-11-03T10:00:00Z", problem.GetProperty("retryAt").GetString());

        for (var n = 0; n < 10; n++)
        {
            using var accepted = await PostAsync(client, $"user{n}@example.com", address: "203.0.113.7", ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var ipLimit = await PostAsync(client, "user10@example.com", address: "203.0.113.7", ct: ct);
        var ipProblem = await Problem.AssertAsync(ipLimit, HttpStatusCode.TooManyRequests, "ip-limit", ct);
        Assert.Equal("2026-11-03T10:00:00Z", ipProblem.GetProperty("retryAt").GetString());
        Assert.False(ipProblem.TryGetProperty("remaining", out _));
    }

    [Fact]
    public async Task Ipv6_addresses_of_one_64_prefix_share_the_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();

        for (var n = 0; n < 10; n++)
        {
            using var accepted = await PostAsync(client, $"user{n}@example.com", address: $"2001:db8:1:2::{n + 1}", ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var sharedPrefix = await PostAsync(client, "other@example.com", address: "2001:db8:1:2:ffff::1", ct: ct);
        await Problem.AssertAsync(sharedPrefix, HttpStatusCode.TooManyRequests, "ip-limit", ct);

        using var otherPrefix = await PostAsync(client, "other@example.com", address: "2001:db8:1:3::1", ct: ct);
        Assert.Equal(HttpStatusCode.Accepted, otherPrefix.StatusCode);
    }

    [Fact]
    public async Task A_refused_request_does_not_count()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();
        const string address = "203.0.113.8";

        for (var n = 0; n < 5; n++)
        {
            using var accepted = await PostAsync(client, "a@example.com", address: address, ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var refused = await PostAsync(client, "a@example.com", address: address, ct: ct);
        await Problem.AssertAsync(refused, HttpStatusCode.TooManyRequests, "email-limit", ct);

        // Five from address X so far; the refused sixth did not count, so five more are accepted and the next is not.
        for (var n = 0; n < 5; n++)
        {
            using var accepted = await PostAsync(client, $"b{n}@example.com", address: address, ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var ipLimit = await PostAsync(client, "c@example.com", address: address, ct: ct);
        await Problem.AssertAsync(ipLimit, HttpStatusCode.TooManyRequests, "ip-limit", ct);
    }

    [Fact]
    public async Task An_unclear_send_counts_as_sent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();
        host.Sender.Next = new CodeEmailOutcome.Unclear();

        using var unclear = await PostAsync(client, "ann@example.com", ct: ct);
        Assert.Equal(HttpStatusCode.Accepted, unclear.StatusCode);
        Assert.Equal("""{"remaining":4,"limit":5}""", await unclear.Content.ReadAsStringAsync(ct));

        host.Sender.Next = new CodeEmailOutcome.Sent();
        using var next = await PostAsync(client, "ann@example.com", ct: ct);
        Assert.Equal(3, (await ReadAsync(next, ct)).GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task Email_service_refusals()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();
        var tomorrow = DateTimeOffset.Parse("2026-11-03T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        host.Sender.Next = new CodeEmailOutcome.Refused(EmailServiceRefusal.DailyLimit, tomorrow);
        using var daily = await PostAsync(client, "ann@example.com", ct: ct);
        var dailyProblem = await Problem.AssertAsync(daily, HttpStatusCode.ServiceUnavailable, "email-service-limit", ct);
        Assert.Equal("2026-11-03T00:00:00Z", dailyProblem.GetProperty("retryAt").GetString());

        host.Sender.Next = new CodeEmailOutcome.Refused(EmailServiceRefusal.Limit, null);
        using var monthly = await PostAsync(client, "ann@example.com", ct: ct);
        var monthlyProblem = await Problem.AssertAsync(monthly, HttpStatusCode.ServiceUnavailable, "email-service-limit", ct);
        Assert.False(monthlyProblem.TryGetProperty("retryAt", out _));

        host.Sender.Next = new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null);
        using var unavailable = await PostAsync(client, "ann@example.com", ct: ct);
        var unavailableProblem = await Problem.AssertAsync(unavailable, HttpStatusCode.ServiceUnavailable, "email-service-unavailable", ct);
        Assert.False(unavailableProblem.TryGetProperty("retryAt", out _));
    }

    [Fact]
    public async Task Invalid_input()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();

        foreach (var email in new[] { "ann", " ann@example.com" })
        {
            using var response = await PostAsync(client, email, ct: ct);
            await Problem.AssertAsync(response, HttpStatusCode.BadRequest, "invalid-email", ct);
        }

        using var locale = await PostAsync(client, "ann@example.com", "de", ct: ct);
        await Problem.AssertAsync(locale, HttpStatusCode.BadRequest, "invalid-locale", ct);
        Assert.Equal(0, host.Sender.Count);
    }

    [Fact]
    public async Task Forwarded_for_is_trusted_only_from_the_proxy_network()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(
            postgres, factory => AtStart(factory.WithSetting("ForwardedHeaders:KnownNetwork", "172.30.0.0/24")));
        using var client = host.CreateClient();

        // A client outside the proxy network cannot choose its address with the header.
        for (var n = 0; n < 10; n++)
        {
            using var accepted = await PostAsync(client, $"user{n}@example.com", address: "198.51.100.1", forwardedFor: $"203.0.113.{n + 20}", ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var spoofed = await PostAsync(client, "user10@example.com", address: "198.51.100.1", forwardedFor: "203.0.113.99", ct: ct);
        await Problem.AssertAsync(spoofed, HttpStatusCode.TooManyRequests, "ip-limit", ct);

        // Through the proxy the address is the one the proxy forwards.
        for (var n = 0; n < 10; n++)
        {
            using var accepted = await PostAsync(client, $"proxied{n}@example.com", address: "172.30.0.5", forwardedFor: "203.0.113.9", ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var proxied = await PostAsync(client, "proxied10@example.com", address: "172.30.0.5", forwardedFor: "203.0.113.9", ct: ct);
        await Problem.AssertAsync(proxied, HttpStatusCode.TooManyRequests, "ip-limit", ct);

        using var otherClient = await PostAsync(client, "proxied11@example.com", address: "172.30.0.5", forwardedFor: "203.0.113.10", ct: ct);
        Assert.Equal(HttpStatusCode.Accepted, otherClient.StatusCode);
    }

    [Fact]
    public async Task Forwarded_for_is_ignored_without_a_proxy_network()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();

        for (var n = 0; n < 10; n++)
        {
            using var accepted = await PostAsync(client, $"user{n}@example.com", address: "198.51.100.1", forwardedFor: $"203.0.113.{n + 20}", ct: ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var spoofed = await PostAsync(client, "user10@example.com", address: "198.51.100.1", forwardedFor: "203.0.113.99", ct: ct);
        await Problem.AssertAsync(spoofed, HttpStatusCode.TooManyRequests, "ip-limit", ct);
    }

    [Fact]
    public async Task Malformed_and_non_json_requests_store_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();

        using var empty = await SendAsync(client, "{}", "application/json", ct);
        await Problem.AssertAsync(empty, HttpStatusCode.BadRequest, "invalid-request", ct);

        using var plain = await SendAsync(client, """{"email":"ann@example.com","locale":"en"}""", "text/plain", ct);
        await Problem.AssertAsync(plain, HttpStatusCode.UnsupportedMediaType, "invalid-request", ct);

        Assert.Equal(0, host.Sender.Count);
        await using var scope = host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(0, await database.Set<Identity.Adapters.Storage.Records.CodeRequestRecord>().CountAsync(ct));
        Assert.Equal(0, await database.Set<Identity.Adapters.Storage.Records.OneTimeCodeRecord>().CountAsync(ct));
    }

    // Review Focus 3: the answer to a code request does not reveal whether the email has a user.
    [Fact]
    public async Task Code_request_answer_does_not_reveal_whether_a_user_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var locales = scope.ServiceProvider.GetRequiredService<ILocales>();
            await scope.ServiceProvider.GetRequiredService<IUsers>().AddOrGetAsync(
                User.SignUp(Email.Create("known@example.com"), TimeZone.FromBrowser("UTC"), Locale.Create("en", locales), Start), ct);
        }

        using var known = await PostAsync(client, "known@example.com", ct: ct);
        using var unknown = await PostAsync(client, "unknown@example.com", ct: ct);

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(ct), await unknown.Content.ReadAsStringAsync(ct));
        Assert.Equal(HeaderNames(known), HeaderNames(unknown));
    }

    [Fact]
    public async Task Development_without_a_key_generates_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, AtStart);
        using var client = host.CreateClient();

        using var response = await PostAsync(client, "ann@example.com", ct: ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Production_without_a_key_does_not_start()
    {
        await using var host = new HostFactory(postgres) { EnvironmentName = Environments.Production }
            .WithSetting("Resend:ApiKey", "re_test_key");
        await host.InitializeAsync();

        var exception = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        Assert.Contains("Identity:CodeHashKey", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_settings_files_hold_no_code_hash_key()
    {
        foreach (var file in Directory.GetFiles(Repository.PathTo("backend/src/HabitsTracker.Host"), "appsettings*.json"))
            Assert.DoesNotContain("CodeHashKey", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configured_limits_replace_the_defaults()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await HostFactory.StartAsync(postgres, factory => AtStart(factory
            .WithSetting("Identity:CodeRequestLimits:PerEmail", "2")
            .WithSetting("Identity:CodeRequestLimits:Window", "01:00:00")));
        using var client = host.CreateClient();

        using var first = await PostAsync(client, "ann@example.com", ct: ct);
        Assert.Equal("""{"remaining":1,"limit":2}""", await first.Content.ReadAsStringAsync(ct));
        using var second = await PostAsync(client, "ann@example.com", ct: ct);
        using var third = await PostAsync(client, "ann@example.com", ct: ct);
        var problem = await Problem.AssertAsync(third, HttpStatusCode.TooManyRequests, "email-limit", ct);
        Assert.Equal("2026-11-02T11:00:00Z", problem.GetProperty("retryAt").GetString());
    }

    private static void AtStart(HostFactory host) => host.WithCapturingSender().Time.SetUtcNow(Start);

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, string email, string locale = "en", string? address = null, string? forwardedFor = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/code-requests")
        {
            Content = JsonContent.Create(new { email, locale }),
        };
        if (address is not null)
            request.Headers.Add(HostFactory.RemoteAddressHeader, address);
        if (forwardedFor is not null)
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request, ct);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string body, string contentType, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/code-requests")
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        return client.SendAsync(request, ct);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken ct) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();

    private static string[] HeaderNames(HttpResponseMessage response) =>
        [.. response.Headers.Concat(response.Content.Headers).Select(header => header.Key).Where(name => name != "Date").Order(StringComparer.Ordinal)];
}
