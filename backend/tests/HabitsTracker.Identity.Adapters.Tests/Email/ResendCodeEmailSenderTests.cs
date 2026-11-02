using System.Net;
using System.Text;
using System.Text.Json;
using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Adapters.Translations;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HabitsTracker.Identity.Adapters.Tests.CodeEmail;

public class ResendCodeEmailSenderTests
{
    private const string ApiKey = "re_test_key";
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly TranslationFiles Files = new(Repository.PathTo("frontend/public/i18n"));

    public static TheoryData<string, HttpStatusCode, string, string?, CodeEmailOutcome> Answers => new()
    {
        { "200", HttpStatusCode.OK, """{"id":"49a3999c-0ce1-4ea6-ab68-afcd6dc2e794"}""", null, new CodeEmailOutcome.Sent() },
        { "daily quota", HttpStatusCode.TooManyRequests, Error("daily_quota_exceeded"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.DailyLimit, new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero)) },
        { "monthly quota", HttpStatusCode.TooManyRequests, Error("monthly_quota_exceeded"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Limit, null) },
        { "rate limit with retry-after", HttpStatusCode.TooManyRequests, Error("rate_limit_exceeded"), "2", new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, Now.AddSeconds(2)) },
        { "rate limit without retry-after", HttpStatusCode.TooManyRequests, Error("rate_limit_exceeded"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "missing api key", HttpStatusCode.Unauthorized, Error("missing_api_key"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "restricted api key", HttpStatusCode.Forbidden, Error("restricted_api_key"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "validation error", HttpStatusCode.UnprocessableContent, Error("validation_error"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "service unavailable", HttpStatusCode.ServiceUnavailable, Error("service_unavailable"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "application error", HttpStatusCode.InternalServerError, Error("application_error"), null, new CodeEmailOutcome.Unclear() },
        { "bad gateway", HttpStatusCode.BadGateway, "", null, new CodeEmailOutcome.Unclear() },
        { "gateway timeout", HttpStatusCode.GatewayTimeout, "", null, new CodeEmailOutcome.Unclear() },
    };

    public static TheoryData<string, Exception, CodeEmailOutcome> Failures => new()
    {
        { "timeout", new TaskCanceledException("The request timed out."), new CodeEmailOutcome.Unclear() },
        { "connection lost after sending", new HttpRequestException(HttpRequestError.ResponseEnded, "The response ended."), new CodeEmailOutcome.Unclear() },
        { "connection refused", new HttpRequestException(HttpRequestError.ConnectionError, "No connection."), new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "name not resolved", new HttpRequestException(HttpRequestError.NameResolutionError, "No such host."), new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
    };

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task Maps_the_answer(string name, HttpStatusCode status, string body, string? retryAfter, CodeEmailOutcome expected)
    {
        Assert.NotEmpty(name);
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfter is not null)
                response.Headers.TryAddWithoutValidation("retry-after", retryAfter);
            return response;
        });

        var outcome = await Send(handler);

        Assert.Equal(expected, outcome);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Maps_a_transport_failure(string name, Exception failure, CodeEmailOutcome expected)
    {
        Assert.NotEmpty(name);
        var handler = new StubHandler(_ => throw failure);

        var outcome = await Send(handler);

        Assert.Equal(expected, outcome);
    }

    // Research R3, R20: a refusal is logged at Warning with the service, the HTTP status and Resend's error name, so a
    // revoked API key shows in the log; never the email, the code or the API key.
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "missing_api_key", "Resend refused the code email: 401 (missing_api_key)")]
    [InlineData(HttpStatusCode.Forbidden, "restricted_api_key", "Resend refused the code email: 403 (restricted_api_key)")]
    [InlineData(HttpStatusCode.UnprocessableContent, "validation_error", "Resend refused the code email: 422 (validation_error)")]
    [InlineData(HttpStatusCode.TooManyRequests, "daily_quota_exceeded", "Resend refused the code email: 429 (daily_quota_exceeded)")]
    [InlineData(HttpStatusCode.ServiceUnavailable, null, "Resend refused the code email: 503 (no error name)")]
    public async Task Logs_a_refusal_at_warning_without_personal_data(HttpStatusCode status, string? errorName, string expected)
    {
        var body = errorName is null ? "<html>down</html>" : Error(errorName);
        var handler = new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        var logs = new CapturingLoggerProvider();

        await Send(handler, logs);

        var line = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal(typeof(ResendCodeEmailSender).FullName, line.Category);
        Assert.Equal(expected, line.Message);
        AssertNoPersonalData(logs);
    }

    [Fact]
    public async Task Logs_an_unreachable_service_at_warning_without_personal_data()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException(HttpRequestError.ConnectionError, "No connection to ann@example.com."));
        var logs = new CapturingLoggerProvider();

        await Send(handler, logs);

        var line = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal("Resend could not be reached: ConnectionError", line.Message);
        Assert.Null(line.Exception);
        AssertNoPersonalData(logs);
    }

    [Fact]
    public async Task An_error_name_that_is_not_a_plain_identifier_is_not_logged()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableContent)
        {
            Content = new StringContent("""{"name":"ann@example.com is invalid"}""", Encoding.UTF8, "application/json"),
        });
        var logs = new CapturingLoggerProvider();

        await Send(handler, logs);

        Assert.Equal("Resend refused the code email: 422 (no error name)", Assert.Single(logs.Entries).Message);
    }

    [Fact]
    public async Task Sends_text_from_the_mail_subdomain()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Send(handler);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.resend.com/emails", request.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(ApiKey, request.Headers.Authorization?.Parameter);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("no-reply@mail.cardlane.eu", root.GetProperty("from").GetString());
        Assert.Equal(["ann@example.com"], root.GetProperty("to").EnumerateArray().Select(element => element.GetString()));
        Assert.Equal(Files.Text(Locale.FromStorage("pl"), "email.subject"), root.GetProperty("subject").GetString());
        Assert.Contains("042917", root.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Equal(["from", "subject", "text", "to"], root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Times_out_after_ten_seconds()
    {
        using var client = new HttpClient();

        ResendCodeEmailSender.ConfigureClient(client, ApiKey);

        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
    }

    private static string Error(string name) => $$"""{"statusCode":0,"name":"{{name}}","message":"m"}""";

    private static void AssertNoPersonalData(CapturingLoggerProvider logs)
    {
        var lines = logs.Entries.Select(entry => string.Join(" | ", [entry.Message, entry.Exception?.ToString() ?? "", .. entry.Scopes])).ToList();
        foreach (var secret in new[] { "ann@example.com", "042917", ApiKey })
            Assert.DoesNotContain(lines, line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<CodeEmailOutcome> Send(StubHandler handler, CapturingLoggerProvider? logs = null)
    {
        using var client = new HttpClient(handler);
        ResendCodeEmailSender.ConfigureClient(client, ApiKey);
        var time = new FakeTimeProvider(Now);
        using var loggers = logs is null ? NullLoggerFactory.Instance : LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var sender = new ResendCodeEmailSender(client, new CodeEmailText(Files.Text), time, loggers.CreateLogger<ResendCodeEmailSender>());

        return await sender.SendAsync(
            Email.Create("ann@example.com"), OneTimeCodeValue.Create("042917"), Locale.FromStorage("pl"), TestContext.Current.CancellationToken);
    }
}
