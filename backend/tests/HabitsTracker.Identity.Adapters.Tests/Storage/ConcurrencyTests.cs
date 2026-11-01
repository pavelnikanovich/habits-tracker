using System.Net;
using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;
using Microsoft.Extensions.DependencyInjection;
using TimeZone = HabitsTracker.Identity.Core.TimeZone;

namespace HabitsTracker.Identity.Adapters.Tests.Storage;

// Concurrent requests on PostgreSQL (data-model.md, "Concurrent changes"): each runs in its own scope and transaction.
public sealed class ConcurrencyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email Ada = Email.Create("ada@example.com");
    private static readonly IpAddressPrefix Address = IpAddressPrefix.From(IPAddress.Parse("192.0.2.1"));

    private IdentityDatabase database = null!;

    public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => database.DisposeAsync();

    [Fact]
    public async Task Concurrent_first_sign_ins_create_one_user()
    {
        var ct = TestContext.Current.CancellationToken;

        var users = await RunInParallelAsync(10, (services, attempt, work) => services.GetRequiredService<IUsers>().AddOrGetAsync(
            User.SignUp(Ada, TimeZone.FromBrowser("Europe/Warsaw"), Locale.FromStorage("en"), Now.AddSeconds(attempt)), work));

        var stored = await database.Get<IUsers>().FindByEmailAsync(Ada, ct);
        Assert.NotNull(stored);
        Assert.All(users, user => Assert.Equal(stored.Id, user.Id));
        Assert.Equal([1], await database.QueryAsync<int>($"""SELECT count(*)::int AS "Value" FROM identity.users"""));
    }

    [Fact]
    public async Task Concurrent_attempt_updates_are_not_lost()
    {
        var ct = TestContext.Current.CancellationToken;
        await database.Get<IOneTimeCodes>().ReplaceAsync(OneTimeCode.Issue(Ada, CodeHash.FromStorage(new byte[32]), Now), ct);

        await RunInParallelAsync(5, async (services, _, work) =>
        {
            var codes = services.GetRequiredService<IOneTimeCodes>();
            var code = await codes.FindExclusiveAsync(Ada, work);
            Assert.NotNull(code);
            await codes.SaveAsync(
                OneTimeCode.FromStorage(code.Email, code.CodeHash, code.IssuedAt, code.ExpiresAt, code.EntryAttempts + 1), work);
            return code.EntryAttempts;
        });

        Assert.Equal([(short)5], await database.QueryAsync<short>($"""SELECT entry_attempts AS "Value" FROM identity.one_time_codes"""));
    }

    [Fact]
    public async Task Concurrent_code_requests_cannot_pass_the_last_free_slot()
    {
        var ct = TestContext.Current.CancellationToken;
        var windowStart = Now - TimeSpan.FromHours(24);
        for (var stored = 0; stored < 4; stored++)
            await database.Get<ICodeRequests>().AddAsync(CodeRequest.Create(Ada, Address, Now - TimeSpan.FromHours(1)), ct);

        await RunInParallelAsync(10, async (services, _, work) =>
        {
            var requests = services.GetRequiredService<ICodeRequests>();
            await requests.LockAsync(Ada, Address, work);
            var counts = await requests.CountAsync(Ada, Address, windowStart, work);
            // Keeps the gap between the count and the insert open, so without the lock the runs overlap in it
            // and more than one passes the last free slot; with the lock they wait, 10 × 50 ms in all.
            await Task.Delay(TimeSpan.FromMilliseconds(50), work);
            if (counts.ForEmail < 5)
                await requests.AddAsync(CodeRequest.Create(Ada, Address, Now), work);
            return counts.ForEmail;
        });

        Assert.Equal(5, (await database.Get<ICodeRequests>().CountAsync(Ada, Address, windowStart, ct)).ForEmail);
    }

    [Fact]
    public async Task Lock_outside_a_transaction_fails()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Get<ICodeRequests>().LockAsync(Ada, Address, ct));
    }

    // Each run is a request of its own: a scope, and in it the work in one transaction.
    private async Task<T[]> RunInParallelAsync<T>(int runs, Func<IServiceProvider, int, CancellationToken, Task<T>> work) =>
        await Task.WhenAll(Enumerable.Range(0, runs).Select(async run =>
        {
            await using var scope = database.OpenScope();
            return await scope.ServiceProvider.GetRequiredService<IIdentityTransaction>().RunAsync(
                inTransaction => work(scope.ServiceProvider, run, inTransaction), TestContext.Current.CancellationToken);
        }));
}
