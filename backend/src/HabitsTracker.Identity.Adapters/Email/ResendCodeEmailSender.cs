using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HabitsTracker.Identity.Core;

namespace HabitsTracker.Identity.Adapters.CodeEmail;

/// <summary>
/// Sends the code email through Resend: text only, from the mail subdomain, no tags, no tracking. This is the
/// anti-corruption layer for the email service: no Resend name or status leaves it, only <see cref="CodeEmailOutcome"/>.
/// A request that did not leave the server is a refusal; one that may have been processed (a <c>500</c>, a timeout,
/// a connection lost after sending) is unclear.
/// </summary>
public sealed class ResendCodeEmailSender(HttpClient client, CodeEmailText text, TimeProvider time) : ICodeEmailSender
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
            // Nothing left the server, so nothing was sent.
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

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return await MapTooManyRequests(response, ct);

        // Resend refused the request (a validation error, an unknown sender) or is down: it sent nothing.
        var refused = (int)response.StatusCode is >= 400 and < 500 || response.StatusCode == HttpStatusCode.ServiceUnavailable;
        return refused ? new CodeEmailOutcome.Refused(EmailServiceRefusal.Unavailable, null) : new CodeEmailOutcome.Unclear();
    }

    private async Task<CodeEmailOutcome> MapTooManyRequests(HttpResponseMessage response, CancellationToken ct)
    {
        switch (await ErrorName(response, ct))
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
            return json.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
