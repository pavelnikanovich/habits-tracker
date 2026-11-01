namespace HabitsTracker.Identity.Core.Tests;

public class OneTimeCodeVerifyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Email Ada = Email.Create("ada@example.com");
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly CodeHash Correct = CodeHash.Compute(Key, Ada, OneTimeCodeValue.Create("123456"));
    private static readonly CodeHash Wrong = CodeHash.Compute(Key, Ada, OneTimeCodeValue.Create("654321"));

    // The table of Task 11; Review Focus 1 is the row at exactly 10 minutes, Review Focus 2 the rows from the fifth
    // entry on.
    [Theory]
    [InlineData(0, true, 9 * 60 + 59, CodeVerification.SignIn, 1)]
    [InlineData(0, true, 10 * 60, CodeVerification.NewCodeRequired, 0)]
    [InlineData(0, false, 60, CodeVerification.Incorrect, 1)]
    [InlineData(3, false, 60, CodeVerification.Incorrect, 4)]
    [InlineData(4, false, 60, CodeVerification.NewCodeRequired, 5)]
    [InlineData(4, true, 60, CodeVerification.SignIn, 5)]
    [InlineData(5, true, 60, CodeVerification.NewCodeRequired, 5)]
    public void Verifies(int attemptsBefore, bool correct, int secondsAfterIssue, CodeVerification expected, int attemptsAfter)
    {
        var code = Stored(attemptsBefore);

        var result = code.Verify(correct ? Correct : Wrong, T0.AddSeconds(secondsAfterIssue));

        Assert.Equal(expected, result);
        Assert.Equal(attemptsAfter, code.EntryAttempts);
        Assert.Equal(expected == CodeVerification.SignIn, code.IsUsed);
    }

    [Fact]
    public void A_used_code_refuses_a_second_entry()
    {
        var code = Stored(0);
        Assert.Equal(CodeVerification.SignIn, code.Verify(Correct, T0.AddMinutes(1)));
        Assert.True(code.IsUsed);

        Assert.Equal(CodeVerification.NewCodeRequired, code.Verify(Correct, T0.AddMinutes(2)));

        Assert.Equal(1, code.EntryAttempts);
    }

    [Fact]
    public void An_entry_after_expiry_changes_nothing()
    {
        var code = Stored(0);

        Assert.Equal(CodeVerification.NewCodeRequired, code.Verify(Correct, T0 + OneTimeCode.Lifetime));

        Assert.Equal(0, code.EntryAttempts);
        Assert.False(code.IsUsed);
    }

    [Fact]
    public void Unset_arguments_are_programming_errors()
    {
        var code = Stored(0);

        Assert.Throws<ArgumentNullException>(() => code.Verify(null!, T0));
        Assert.Throws<ArgumentException>(() => code.Verify(Correct, default));
        Assert.Throws<ArgumentException>(
            () => code.Verify(Correct, new DateTimeOffset(2026, 11, 2, 11, 0, 0, TimeSpan.FromHours(1))));
    }

    private static OneTimeCode Stored(int entryAttempts) =>
        OneTimeCode.FromStorage(Ada, Correct, T0, T0 + OneTimeCode.Lifetime, entryAttempts);
}
