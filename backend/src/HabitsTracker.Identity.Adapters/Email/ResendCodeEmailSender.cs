using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HabitsTracker.Identity.Core;
using Microsoft.Extensions.Logging;

namespace HabitsTracker.Identity.Adapters.CodeEmail;

/// <summary>
/// Sends the code email through Resend: text only, from the mail subdomain, no tags, no tracking. This is the
/// anti-corruption layer for the email service: no Resend name or status leaves it, only <see cref="CodeEmailOutcome"/>.
/// A request that did not leave the server is a refusal; one that may have been processed (a <c>500</c>, a timeout,
/// a connection lost after sending) is unclear. Every refusal is logged at Warning with the HTTP status and Resend's
/// error name, so a revoked API key shows in the log; never the email, the code, the text or the API key (research R3, R20).
/// </summary>
public sealed partial class ResendCodeEmailSender(HttpClient client, CodeEmailText text, TimeProvider time, ILogger<ResendCodeEmailSender> logger)
    : ICodeEmailSender
{
    private const string Endpoint = "https://api.resend.com/emails";
    private const string From = "no-reply@mail.cardlane.eu";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Sets the timeout and the API key of the client the sender sends with.</summary>
    public static void ConfigureClient(HttpClient client, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        client.Timeout = Timeout;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<CodeEmailOutcome> SendAsync(Email email, OneTimeCodeValue code, Locale locale, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(locale);

        var (subject, body) = text.Compose(locale, code);
        try
        {
            using var response = await client.PostAsJsonAsync(
                Endpoint, new { from = From, to = new[] { email.Value }, subject, text = body }, ct);
            return await Map(response, ct);
        }
        catch (HttpRequestException exception) when (exception.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError)
        {
            // Nothing left the server, so nothing was sent. The exception is not logged: its message may name more
            // than the error.
            LogUnreachable(logger, exception.HttpRequestError);
            return new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null);
        }
        catch (HttpRequestException)
        {
            return new CodeEmailOutcome.Unclear();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The timeout of the client: the email may be on its way.
            return new CodeEmailOutcome.Unclear();
        }
    }

    private async Task<CodeEmailOutcome> Map(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return new CodeEmailOutcome.Sent();

        // Resend refused the request (a validation error, an unknown sender, a wrong API key) or is down: it sent
        // nothing. Any other answer (another 5xx) may come after the email was processed.
        var status = (int)response.StatusCode;
        if (status is not (>= 400 and < 500) && response.StatusCode != HttpStatusCode.ServiceUnavailable)
            return new CodeEmailOutcome.Unclear();

        var errorName = await ErrorName(response, ct);
        LogRefused(logger, status, errorName ?? "no error name");

        return response.StatusCode == HttpStatusCode.TooManyRequests
            ? MapTooManyRequests(response, errorName)
            : new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null);
    }

    private CodeEmailOutcome.Refused MapTooManyRequests(HttpResponseMessage response, string? errorName)
    {
        switch (errorName)
        {
            case "daily_quota_exceeded":
                var now = time.GetUtcNow();
                return new CodeEmailOutcome.Refused(EmailServiceRefusal.DailyLimit, new DateTimeOffset(now.Date, TimeSpan.Zero).AddDays(1));
            case "monthly_quota_exceeded":
                return new CodeEmailOutcome.Refused(EmailServiceRefusal.Limit, null);
            default:
                var wait = response.Headers.RetryAfter?.Delta;
                return new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, wait is null ? null : time.GetUtcNow() + wait);
        }
    }

    private static async Task<string?> ErrorName(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var name = json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("name", out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : "";
            return IsPlainIdentifier(name) ? name : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Resend's error names are identifiers such as missing_api_key; anything else is not logged, as it could hold the
    // email of the request.
    private static bool IsPlainIdentifier(string name) =>
        name.Length is > 0 and <= 64 && name.All(character => character is (>= 'a' and <= 'z') or '_');

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resend refused the code email: {Status} ({ErrorName})")]
    private static partial void LogRefused(ILogger logger, int status, string errorName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resend could not be reached: {Error}")]
    private static partial void LogUnreachable(ILogger logger, HttpRequestError error);
}
