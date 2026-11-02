using HabitsTracker.Identity.Core;

namespace HabitsTracker.Tests.PortContracts;

// The contract of ILocales (FR-020): the translation files and the in-memory fake pass it, both holding en, pl and ru.
public abstract class LocalesContract
{
    protected abstract ILocales Locales { get; }

    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    [InlineData("ru")]
    public void A_locale_with_a_translation_file_is_available(string code) =>
        Assert.True(Locales.IsAvailable(code));

    [Theory]
    [InlineData("de")]
    [InlineData("PL")]
    [InlineData("")]
    public void Any_other_code_is_not_available(string code) =>
        Assert.False(Locales.IsAvailable(code));
}
