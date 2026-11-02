using HabitsTracker.Identity.Adapters.CodeEmail;
using HabitsTracker.Identity.Adapters.Translations;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;

namespace HabitsTracker.Identity.Adapters.Tests.CodeEmail;

public class CodeEmailTextTests
{
    private static readonly TranslationFiles Files = new(Repository.PathTo("frontend/public/i18n"));

    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    [InlineData("ru")]
    public void Composes_in_each_locale(string code)
    {
        var locale = Locale.FromStorage(code);
        var digits = OneTimeCodeValue.Create("042917");

        var (subject, body) = new CodeEmailText(Files.Text).Compose(locale, digits);

        Assert.Equal(Files.Text(locale, "email.subject"), subject);
        Assert.Equal(Files.Text(locale, "email.body").Replace("{code}", "042917", StringComparison.Ordinal), body);
        Assert.Contains("042917", body, StringComparison.Ordinal);
        Assert.DoesNotContain("{code}", subject + body, StringComparison.Ordinal);
    }
}
