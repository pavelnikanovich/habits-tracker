namespace HabitsTracker.Identity.Core.Tests;

public class OneTimeCodeValueTests
{
    [Fact]
    public void Creates_six_digits()
    {
        Assert.Equal("042917", OneTimeCodeValue.Create("042917").Digits);
    }

    [Theory]
    [InlineData("42917")]
    [InlineData("0429170")]
    [InlineData("04291a")]
    [InlineData(" 042917")]
    [InlineData("042917\n")]
    // Full-width digits: ASCII only.
    [InlineData("０４２９１７")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects(string? input)
    {
        var exception = Assert.Throws<DomainException>(() => OneTimeCodeValue.Create(input));

        Assert.Equal(DomainError.InvalidOneTimeCode, exception.Error);
    }

    [Fact]
    public void Generated_codes_are_six_ascii_digits()
    {
        for (var i = 0; i < 1000; i++)
            Assert.Matches("^[0-9]{6}$", OneTimeCodeValue.Generate().Digits);
    }
}
