using System.Net;
using HabitsTracker.Identity.Core.Tests.Fakes;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.Extensions.Time.Testing;

namespace HabitsTracker.Identity.Core.Tests;

public class DeleteExpiredRecordsTests
{
    private static readonly DateTimeOffset N = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    private static readonly Email Ada = Email.Create("ada@example.com");
    private static readonly IpAddressPrefix Prefix = IpAddressPrefix.From(IPAddress.Parse("203.0.113.7"));
    private static readonly CodeHash Hash = CodeHash.FromStorage(new byte[32]);
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

    private readonly FakeTimeProvider time = new(N);
    private readonly InMemoryOneTimeCodes oneTimeCodes;
    private readonly InMemoryCodeRequests codeRequests;
    private readonly InMemorySessions sessions;

    public DeleteExpiredRecordsTests()
    {
        var transaction = new InMemoryTransaction();
        oneTimeCodes = new InMemoryOneTimeCodes(transaction);
        codeRequests = new InMemoryCodeRequests(transaction);
        sessions = new InMemorySessions(transaction);
    }

    [Fact]
    public async Task Deletes_what_is_past_its_period()
    {
        var expiresNow = await StoreCodeAsync("expires-now@example.com", issuedAt: N - TimeSpan.FromMinutes(10));
        var expiresLater = await StoreCodeAsync("expires-later@example.com", issuedAt: N - TimeSpan.FromMinutes(10) + Second);
        var exhausted = await StoreCodeAsync("exhausted@example.com", issuedAt: N - TimeSpan.FromMinutes(5), entryAttempts: 5);
        await codeRequests.AddAsync(CodeRequest.Create(Ada, Prefix, N - TimeSpan.FromHours(24)), Ct);
        await codeRequests.AddAsync(CodeRequest.Create(Ada, Prefix, N - TimeSpan.FromHours(24) + Second), Ct);
        var inactive = await OpenAsync(lastActivityAt: N - TimeSpan.FromDays(30));
        var active = await OpenAsync(lastActivityAt: N - TimeSpan.FromDays(30) + Second);

        Assert.Equal(new DeletedRecords(2, 1, 1), await DeleteAsync());

        Assert.Null(await oneTimeCodes.FindExclusiveAsync(expiresNow, Ct));
        Assert.NotNull(await oneTimeCodes.FindExclusiveAsync(expiresLater, Ct));
        Assert.Null(await oneTimeCodes.FindExclusiveAsync(exhausted, Ct));
        var kept = N - TimeSpan.FromHours(24) + Second;
        Assert.Equal(new CodeRequestCounts(1, kept, 1, kept), await codeRequests.CountAsync(Ada, Prefix, N - TimeSpan.FromDays(2), Ct));
        Assert.Null(await sessions.FindAsync(inactive.Id, Ct));
        Assert.NotNull(await sessions.FindAsync(active.Id, Ct));
    }

    [Fact]
    public async Task Passes_the_core_rules_to_the_stores()
    {
        await DeleteAsync();

        Assert.Equal<(DateTimeOffset, int)?>((N, 5), oneTimeCodes.DeletedExpiredOrExhaustedWith);
        Assert.Equal(N - TimeSpan.FromHours(24), codeRequests.DeletedRequestedAtOrBefore);
        Assert.Equal(N - TimeSpan.FromDays(30), sessions.DeletedInactiveAtOrBefore);
    }

    // Beyond the plan, as for RequestCode (Task 7 review): a hard-coded 24 hours passes the test above.
    [Fact]
    public async Task Uses_the_configured_window()
    {
        await DeleteAsync(CodeRequestLimits.Create(5, 10, TimeSpan.FromHours(1)));

        Assert.Equal(N - TimeSpan.FromHours(1), codeRequests.DeletedRequestedAtOrBefore);
    }

    [Fact]
    public void Unset_dependencies_are_programming_errors()
    {
        var options = IdentityContextOptions.Create(Key, CodeRequestLimits.Default);

        Assert.Throws<ArgumentNullException>("oneTimeCodes", () => new DeleteExpiredRecords(null!, codeRequests, sessions, time, options));
        Assert.Throws<ArgumentNullException>("codeRequests", () => new DeleteExpiredRecords(oneTimeCodes, null!, sessions, time, options));
        Assert.Throws<ArgumentNullException>("sessions", () => new DeleteExpiredRecords(oneTimeCodes, codeRequests, null!, time, options));
        Assert.Throws<ArgumentNullException>("time", () => new DeleteExpiredRecords(oneTimeCodes, codeRequests, sessions, null!, options));
        Assert.Throws<ArgumentNullException>("options", () => new DeleteExpiredRecords(oneTimeCodes, codeRequests, sessions, time, null!));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<DeletedRecords> DeleteAsync(CodeRequestLimits? limits = null) =>
        new DeleteExpiredRecords(oneTimeCodes, codeRequests, sessions, time, IdentityContextOptions.Create(Key, limits ?? CodeRequestLimits.Default))
            .ExecuteAsync(Ct);

    // A code expires 10 minutes after issue (OneTimeCode.Lifetime).
    private async Task<Email> StoreCodeAsync(string email, DateTimeOffset issuedAt, int entryAttempts = 0)
    {
        var address = Email.Create(email);
        await oneTimeCodes.ReplaceAsync(
            OneTimeCode.FromStorage(address, Hash, issuedAt, issuedAt + TimeSpan.FromMinutes(10), entryAttempts), Ct);
        return address;
    }

    private async Task<Session> OpenAsync(DateTimeOffset lastActivityAt)
    {
        var (session, _) = Session.Open(Guid.CreateVersion7(lastActivityAt), false, lastActivityAt);
        await sessions.AddAsync(session, Ct);
        return session;
    }
}
