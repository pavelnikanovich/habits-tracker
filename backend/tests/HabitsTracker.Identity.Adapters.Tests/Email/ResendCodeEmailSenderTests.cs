using System.Net;
using System.Text;
using System.Text.Json;
using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Adapters.Translations;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
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
        { "validation error", HttpStatusCode.UnprocessableContent, Error("validation_error"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "service unavailable", HttpStatusCode.ServiceUnavailable, Error("service_unavailable"), null, new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) },
        { "application error", HttpStatusCode.InternalServerError, Error("application_error"), null, new CodeEmailOutcome.Unclear() },
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

    private static async Task<CodeEmailOutcome> Send(StubHandler handler)
    {
        using var client = new HttpClient(handler);
        ResendCodeEmailSender.ConfigureClient(client, ApiKey);
        var time = new FakeTimeProvider(Now);
        var sender = new ResendCodeEmailSender(client, new CodeEmailText(Files.Text), time);

        return await sender.SendAsync(
            Email.Create("ann@example.com"), OneTimeCodeValue.Create("042917"), Locale.FromStorage("pl"), TestContext.Current.CancellationToken);
    }
}
