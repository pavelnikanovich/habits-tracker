namespace HabitsTracker.Identity.Core.Tests;

public class LocaleTests
{
    private static readonly FakeLocales Available = new("en", "pl", "ru");

    [Fact]
    public void Creates_an_available_locale()
    {
        Assert.Equal("pl", Locale.Create("pl", Available).Code);
    }

    [Theory]
    // No translation file.
    [InlineData("de")]
    [InlineData("PL")]
    [InlineData("pl-PL")]
    [InlineData("pl\n")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects(string? code)
    {
        var exception = Assert.Throws<DomainException>(() => Locale.Create(code, Available));

        Assert.Equal(DomainError.InvalidLocale, exception.Error);
    }

    [Fact]
    public void From_storage_checks_the_pattern_only()
    {
        Assert.Equal("de", Locale.FromStorage("de").Code);

        foreach (var code in new[] { "PL", "" })
        {
            var exception = Assert.Throws<DomainException>(() => Locale.FromStorage(code));

            Assert.Equal(DomainError.CorruptRecord, exception.Error);
        }
    }

    private sealed class FakeLocales(params string[] codes) : ILocales
    {
        public bool IsAvailable(string code) => codes.Contains(code);
    }
}
