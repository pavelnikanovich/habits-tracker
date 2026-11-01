using System.Net;
using System.Text;
using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Adapters.Translations;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using HabitsTracker.Tests.PortContracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HabitsTracker.Identity.Adapters.Tests.CodeEmail;

// The senders pass the contract of ICodeEmailSender; the Resend sender sits behind a stub of the email service.
public static class SenderContractTests
{
    private static readonly TranslationFiles Files = new(Repository.PathTo("frontend/public/i18n"));

    public sealed class DevelopmentLog : CodeEmailSenderContract
    {
        protected override ICodeEmailSender DeliveringSender { get; } =
            new DevelopmentLogCodeEmailSender(new CodeEmailText(Files.Text), NullLogger<DevelopmentLogCodeEmailSender>.Instance);
    }

    public sealed class Resend : RefusingCodeEmailSenderContract
    {
        protected override ICodeEmailSender DeliveringSender => Behind(HttpStatusCode.OK, """{"id":"1"}""");

        protected override (ICodeEmailSender Sender, CodeEmailOutcome.Refused Refusal) Refusing =>
            (Behind(HttpStatusCode.TooManyRequests, """{"name":"daily_quota_exceeded"}"""),
                new CodeEmailOutcome.Refused(EmailServiceRefusal.DailyLimit, new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero)));

        private static ResendCodeEmailSender Behind(HttpStatusCode status, string body)
        {
            var client = new HttpClient(new StubHandler(_ =>
                new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
            ResendCodeEmailSender.ConfigureClient(client, "re_test_key");
            return new ResendCodeEmailSender(
                client, new CodeEmailText(Files.Text), new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero)));
        }
    }
}
