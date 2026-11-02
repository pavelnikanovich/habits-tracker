using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Adapters.Translations;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.Extensions.Logging;

namespace HabitsTracker.Identity.Adapters.Tests.CodeEmail;

public class DevelopmentLogCodeEmailSenderTests
{
    [Fact]
    public async Task Writes_the_email_to_the_log()
    {
        var ct = TestContext.Current.CancellationToken;
        var files = new TranslationFiles(Repository.PathTo("frontend/public/i18n"));
        var russian = Locale.FromStorage("ru");
        using var provider = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var sender = new DevelopmentLogCodeEmailSender(new CodeEmailText(files.Text), factory.CreateLogger<DevelopmentLogCodeEmailSender>());

        var outcome = await sender.SendAsync(Email.Create("ann@example.com"), OneTimeCodeValue.Create("042917"), russian, ct);

        Assert.Equal(new CodeEmailOutcome.Sent(), outcome);
        var entry = Assert.Single(provider.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        var lines = entry.Message.Split('\n');
        Assert.Equal($"code-email to=ann@example.com locale=ru code=042917 subject={files.Text(russian, "email.subject")}", lines[0]);
        Assert.Contains("042917", string.Join('\n', lines[1..]), StringComparison.Ordinal);
    }
}
