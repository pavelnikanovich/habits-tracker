using HabitsTracker.Identity.Core;
using HabitsTracker.Tests;

namespace HabitsTracker.Identity.Adapters.Tests.Storage;

// Loading goes through the core's FromStorage factories, so a row that breaks a rule is refused
// (data-model.md, "Domain model rules").
public sealed class LoadTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private IdentityDatabase database = null!;

    public async ValueTask InitializeAsync() => database = await IdentityDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => database.DisposeAsync();

    [Fact]
    public async Task Corrupt_rows_are_rejected_on_load()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.CreateVersion7(Now);
        var sessionId = new byte[32];
        var expiresAt = Now + OneTimeCode.Lifetime;
        await database.ExecuteAsync($"""
            INSERT INTO identity.users (id, email, time_zone, time_zone_recognized, locale, created_at)
            VALUES ({userId}, 'ada@example.com', 'Europe/Warsaw', true, 'en', {Now}),
                   ({Guid.CreateVersion7(Now)}, 'bob@example.com', 'Europe/Warsaw', true, 'PL', {Now})
            """);
        await database.ExecuteAsync($"""
            INSERT INTO identity.one_time_codes (email, code_hash, issued_at, expires_at, entry_attempts)
            VALUES ('ada@example.com', {new byte[32]}, {Now}, {expiresAt}, 7),
                   ('bob@example.com', {new byte[31]}, {Now}, {expiresAt}, 0)
            """);
        await database.ExecuteAsync($"""
            INSERT INTO identity.sessions (id, user_id, remembered, created_at, last_activity_at)
            VALUES ({sessionId}, {userId}, false, {Now}, {Now - TimeSpan.FromSeconds(1)})
            """);

        await AssertCorruptAsync(() => database.Get<IOneTimeCodes>().FindExclusiveAsync(Email.Create("ada@example.com"), ct));
        await AssertCorruptAsync(() => database.Get<IOneTimeCodes>().FindExclusiveAsync(Email.Create("bob@example.com"), ct));
        await AssertCorruptAsync(() => database.Get<ISessions>().FindAsync(sessionId, ct));
        await AssertCorruptAsync(() => database.Get<IUsers>().FindByEmailAsync(Email.Create("bob@example.com"), ct));
    }

    private static async Task AssertCorruptAsync(Func<Task> load)
    {
        var exception = await Assert.ThrowsAsync<DomainException>(load);
        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }
}
