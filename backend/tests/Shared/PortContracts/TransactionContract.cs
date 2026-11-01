using System.Net;
using HabitsTracker.Identity.Core;
using TimeZone = HabitsTracker.Identity.Core.TimeZone;

namespace HabitsTracker.Tests.PortContracts;

// The contract of IIdentityTransaction (data-model.md, "Driven ports"): the PostgreSQL transaction and the
// in-memory fake pass it. The stores are those the transaction wraps.
public abstract class TransactionContract
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email Ada = Email.Create("ada@example.com");

    protected abstract IIdentityTransaction Transaction { get; }

    protected abstract IUsers Users { get; }

    protected abstract IOneTimeCodes OneTimeCodes { get; }

    protected abstract ICodeRequests CodeRequests { get; }

    [Fact]
    public async Task Work_that_returns_is_committed()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = User.SignUp(Ada, TimeZone.FromBrowser("Europe/Warsaw"), Locale.FromStorage("en"), Now);

        var result = await Transaction.RunAsync(
            async work =>
            {
                await Users.AddOrGetAsync(user, work);
                return 42;
            },
            ct);

        Assert.Equal(42, result);
        Assert.NotNull(await Users.FindByEmailAsync(Ada, ct));
    }

    [Fact]
    public async Task Work_that_throws_is_rolled_back()
    {
        var ct = TestContext.Current.CancellationToken;
        var address = IpAddressPrefix.From(IPAddress.Parse("192.0.2.1"));
        var failure = new InvalidOperationException("The work failed.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Transaction.RunAsync<int>(
            async work =>
            {
                await Users.AddOrGetAsync(User.SignUp(Ada, TimeZone.FromBrowser("Europe/Warsaw"), Locale.FromStorage("en"), Now), work);
                await OneTimeCodes.ReplaceAsync(OneTimeCode.Issue(Ada, CodeHash.FromStorage(new byte[32]), Now), work);
                await CodeRequests.AddAsync(CodeRequest.Create(Ada, address, Now), work);
                throw failure;
            },
            ct));

        Assert.Same(failure, thrown);
        Assert.Null(await Users.FindByEmailAsync(Ada, ct));
        Assert.Null(await OneTimeCodes.FindExclusiveAsync(Ada, ct));
        Assert.Equal(0, (await CodeRequests.CountAsync(Ada, address, Now - TimeSpan.FromDays(1), ct)).ForEmail);
    }

    [Fact]
    public async Task Rollback_puts_back_the_rows_the_work_replaced_or_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var address = IpAddressPrefix.From(IPAddress.Parse("192.0.2.1"));
        var earlier = OneTimeCode.Issue(Ada, CodeHash.FromStorage(new byte[32]), Now);
        await OneTimeCodes.ReplaceAsync(earlier, ct);
        await CodeRequests.AddAsync(CodeRequest.Create(Ada, address, Now), ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Transaction.RunAsync<int>(
            async work =>
            {
                // Two replacements of one row: the rollback must undo the newest first to reach the earlier code.
                await OneTimeCodes.ReplaceAsync(OneTimeCode.Issue(Ada, HashOf(1), Now.AddMinutes(1)), work);
                await OneTimeCodes.ReplaceAsync(OneTimeCode.Issue(Ada, HashOf(2), Now.AddMinutes(2)), work);
                await CodeRequests.DeleteRequestedAtOrBeforeAsync(Now, work);
                throw new InvalidOperationException("The work failed.");
            },
            ct));

        var stored = await OneTimeCodes.FindExclusiveAsync(Ada, ct);
        Assert.NotNull(stored);
        Assert.True(stored.CodeHash.Matches(earlier.CodeHash));
        Assert.Equal(earlier.IssuedAt, stored.IssuedAt);
        Assert.Equal(
            new CodeRequestCounts(1, Now, 1, Now),
            await CodeRequests.CountAsync(Ada, address, Now - TimeSpan.FromDays(1), ct));
    }

    private static CodeHash HashOf(byte fill) => CodeHash.FromStorage(Enumerable.Repeat(fill, 32).ToArray());
}
