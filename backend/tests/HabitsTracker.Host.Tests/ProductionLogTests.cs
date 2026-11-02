using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HabitsTracker.Identity.Adapters.Storage;
using HabitsTracker.Identity.Adapters.Storage.Records;
using HabitsTracker.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace HabitsTracker.Host.Tests;

// SC-005, research R20: the logs of the Production host hold no email, user id, session token or one-time code, and a
// 500 carries the trace id of its log line. Every category is captured down to Trace, a superset of what Production
// writes, with the scopes the console logger writes with IncludeScopes.
public class ProductionLogTests(PostgresFixture postgres)
{
    private const string Ann = "ann@example.com";

    [Fact]
    public async Task No_secret_or_email_reaches_the_production_logs()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var host = await HostFactory.StartAsync(postgres, InProduction);
        await using var logged = WithLogs(host, logs);
        using var client = logged.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using (var requested = await client.PostAsJsonAsync("/api/code-requests", new { email = Ann, locale = "en" }, ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = host.Sender.LastCode(Ann)!;
        var wrongCode = code == "000000" ? "000001" : "000000";
        using (var wrong = await client.PostAsJsonAsync("/api/sessions", SignInBody(wrongCode), ct))
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var signedIn = await client.PostAsJsonAsync("/api/sessions", SignInBody(code), ct);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var token = SetCookieHeaderValue.Parse(Assert.Single(signedIn.Headers.GetValues(HeaderNames.SetCookie))).Value.ToString();
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        me.Headers.Add(HeaderNames.Cookie, $"session={token}");
        using (var current = await client.SendAsync(me, ct))
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        string userId;
        await using (var scope = logged.Services.CreateAsyncScope())
            userId = (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<UserRecord>().SingleAsync(ct)).Id.ToString();

        var lines = logs.Entries.Select(entry => string.Join(" | ", [entry.Message, entry.Exception?.ToString() ?? "", .. entry.Scopes])).ToList();
        // Not vacuous: the requests were logged, with their scopes.
        Assert.Contains(lines, line => line.Contains("/api/sessions", StringComparison.Ordinal) && line.Contains("TraceId:", StringComparison.Ordinal));
        foreach (var secret in new[] { Ann, token, userId })
            Assert.DoesNotContain(lines, line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
        foreach (var digits in new[] { code, wrongCode })
            Assert.DoesNotContain(lines, line => Regex.IsMatch(line, $@"\b{digits}\b", RegexOptions.None, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task A_500_and_its_log_line_carry_the_same_trace_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var host = await HostFactory.StartAsync(postgres, InProduction);
        await using var logged = WithLogs(host, logs);
        using var client = logged.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/test/throw", UriKind.Relative), ct);

        var problem = await Problem.AssertAsync(response, HttpStatusCode.InternalServerError, "internal-error", ct);
        var traceId = problem.GetProperty("traceId").GetString()!;
        var failure = Assert.Single(logs.Entries, entry => entry.Exception is InvalidOperationException { Message: "secret detail" });
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Contains(failure.Scopes, scope => scope.Contains($"TraceId:{traceId}", StringComparison.Ordinal));
    }

    // The console logger writes the scopes, so the trace id is on every line of a request (research R20).
    [Fact]
    public async Task The_console_log_includes_scopes()
    {
        await using var host = await HostFactory.StartAsync(postgres, InProduction);

        Assert.True(host.Services.GetRequiredService<IOptionsMonitor<SimpleConsoleFormatterOptions>>().CurrentValue.IncludeScopes);
    }

    // Not every SQL command at Information in the journal: EF Core writes from Warning up. The capture above is
    // unaffected, as it takes every category down to Trace.
    [Fact]
    public async Task Production_logs_ef_core_from_warning_up()
    {
        await using var host = await HostFactory.StartAsync(postgres, InProduction);

        var commands = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");

        Assert.False(commands.IsEnabled(LogLevel.Information));
        Assert.True(commands.IsEnabled(LogLevel.Warning));
    }

    private static void InProduction(HostFactory host)
    {
        host.EnvironmentName = Environments.Production;
        host.WithCapturingSender()
            // Ruling R-5: Production neither migrates nor starts without these; the capturing sender replaces Resend.
            .WithSetting("Database:MigrateOnStart", "true")
            .WithSetting("Identity:CodeHashKey", Convert.ToBase64String(new byte[32]))
            .WithSetting("Resend:ApiKey", "re_test_key");
    }

    // The host with every category captured down to Trace; the console keeps the levels of the settings files.
    private static WebApplicationFactory<Program> WithLogs(HostFactory host, CapturingLoggerProvider logs) =>
        host.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(logs);
            logging.AddFilter<CapturingLoggerProvider>(category: null, LogLevel.Trace);
        }));

    private static object SignInBody(string code) => new { email = Ann, code, rememberMe = false, timeZone = "UTC", locale = "en" };
}
