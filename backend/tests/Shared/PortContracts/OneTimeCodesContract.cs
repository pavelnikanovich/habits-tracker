using HabitsTracker.Identity.Core;

namespace HabitsTracker.Tests.PortContracts;

// The contract of IOneTimeCodes (data-model.md, "Driven ports"): the PostgreSQL store and the in-memory fake pass it.
public abstract class OneTimeCodesContract
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email Ada = Email.Create("ada@example.com");

    protected abstract IOneTimeCodes OneTimeCodes { get; }

    [Fact]
    public async Task Replace_keeps_one_code_per_email()
    {
        var ct = TestContext.Current.CancellationToken;
        var earlier = Code(Ada, Now, hashFill: 1);
        var later = Code(Ada, Now.AddMinutes(1), hashFill: 2);
        await OneTimeCodes.ReplaceAsync(earlier, ct);

        await OneTimeCodes.ReplaceAsync(later, ct);

        AssertStored(later, await OneTimeCodes.FindExclusiveAsync(Ada, ct));
    }

    [Fact]
    public async Task Saving_stores_the_attempts()
    {
        var ct = TestContext.Current.CancellationToken;
        await OneTimeCodes.ReplaceAsync(Code(Ada, Now), ct);
        var attempted = Code(Ada, Now, entryAttempts: 3);

        await OneTimeCodes.SaveAsync(attempted, ct);

        AssertStored(attempted, await OneTimeCodes.FindExclusiveAsync(Ada, ct));
    }

    [Fact]
    public async Task Saving_a_used_code_deletes_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var used = Code(Ada, Now);
        var other = Code(Email.Create("bob@example.com"), Now);
        await OneTimeCodes.ReplaceAsync(used, ct);
        await OneTimeCodes.ReplaceAsync(other, ct);
        Assert.Equal(CodeVerification.SignIn, used.Verify(used.CodeHash, Now.AddMinutes(1)));

        await OneTimeCodes.SaveAsync(used, ct);

        Assert.Null(await OneTimeCodes.FindExclusiveAsync(Ada, ct));
        AssertStored(other, await OneTimeCodes.FindExclusiveAsync(other.Email, ct));
    }

    [Fact]
    public async Task Delete_expired_or_exhausted()
    {
        var ct = TestContext.Current.CancellationToken;
        var expiresNow = Code(Email.Create("expires-now@example.com"), Now - OneTimeCode.Lifetime);
        var expiresLater = Code(Email.Create("expires-later@example.com"), Now - OneTimeCode.Lifetime + TimeSpan.FromSeconds(1));
        var fiveAttempts = Code(Email.Create("five@example.com"), Now, entryAttempts: 5);
        var fourAttempts = Code(Email.Create("four@example.com"), Now, entryAttempts: 4);
        var threeAttempts = Code(Email.Create("three@example.com"), Now, entryAttempts: 3);
        foreach (var code in new[] { expiresNow, expiresLater, fiveAttempts, fourAttempts, threeAttempts })
            await OneTimeCodes.ReplaceAsync(code, ct);

        Assert.Equal(2, await OneTimeCodes.DeleteExpiredOrExhaustedAsync(Now, 5, ct));

        Assert.Null(await OneTimeCodes.FindExclusiveAsync(expiresNow.Email, ct));
        AssertStored(expiresLater, await OneTimeCodes.FindExclusiveAsync(expiresLater.Email, ct));
        Assert.Null(await OneTimeCodes.FindExclusiveAsync(fiveAttempts.Email, ct));
        AssertStored(fourAttempts, await OneTimeCodes.FindExclusiveAsync(fourAttempts.Email, ct));
        AssertStored(threeAttempts, await OneTimeCodes.FindExclusiveAsync(threeAttempts.Email, ct));

        Assert.Equal(2, await OneTimeCodes.DeleteExpiredOrExhaustedAsync(Now, 3, ct));

        Assert.Null(await OneTimeCodes.FindExclusiveAsync(fourAttempts.Email, ct));
        Assert.Null(await OneTimeCodes.FindExclusiveAsync(threeAttempts.Email, ct));
        AssertStored(expiresLater, await OneTimeCodes.FindExclusiveAsync(expiresLater.Email, ct));
    }

    private static OneTimeCode Code(Email email, DateTimeOffset issuedAt, int entryAttempts = 0, byte hashFill = 1) =>
        OneTimeCode.FromStorage(
            email,
            CodeHash.FromStorage(Enumerable.Repeat(hashFill, 32).ToArray()),
            issuedAt,
            issuedAt + OneTimeCode.Lifetime,
            entryAttempts);

    private static void AssertStored(OneTimeCode expected, OneTimeCode? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Email, actual.Email);
        Assert.Equal(expected.CodeHash, actual.CodeHash);
        Assert.Equal(expected.IssuedAt, actual.IssuedAt);
        Assert.Equal(expected.ExpiresAt, actual.ExpiresAt);
        Assert.Equal(expected.EntryAttempts, actual.EntryAttempts);
    }
}
