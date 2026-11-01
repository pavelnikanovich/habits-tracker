namespace HabitsTracker.Identity.Core.Tests.Fakes;

// IOneTimeCodes in memory, as EfOneTimeCodes behaves: one code per email, a new OneTimeCode on every read.
// FindExclusiveAsync holds nothing: the use case tests make one call at a time, and the row lock is tested in
// the storage adapter's concurrency tests.
public sealed class InMemoryOneTimeCodes : IOneTimeCodes
{
    private readonly Dictionary<Email, Row> rows = [];
    private readonly InMemoryTransaction transaction;
    private readonly CallLog? calls;

    // calls: null when a test does not check the order of calls.
    public InMemoryOneTimeCodes(InMemoryTransaction transaction, CallLog? calls = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        this.transaction = transaction;
        this.calls = calls;
    }

    public Task<OneTimeCode?> FindExclusiveAsync(Email email, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(email);
        return Task.FromResult(rows.TryGetValue(email, out var row) ? row.ToOneTimeCode() : null);
    }

    public Task ReplaceAsync(OneTimeCode code, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(code);
        transaction.Write(rows, code.Email, Row.From(code));
        return Task.CompletedTask;
    }

    // A used code is deleted; otherwise only the entry attempts change, as in EfOneTimeCodes.
    public Task SaveAsync(OneTimeCode code, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(code);

        if (code.IsUsed)
            transaction.Write(rows, code.Email, null);
        else if (rows.TryGetValue(code.Email, out var row))
            transaction.Write(rows, code.Email, row with { EntryAttempts = code.EntryAttempts });

        return Task.CompletedTask;
    }

    public Task<int> DeleteExpiredOrExhaustedAsync(DateTimeOffset now, int maxEntryAttempts, CancellationToken ct)
    {
        calls?.Record(this);
        var deleted = rows.Values.Where(row => row.ExpiresAt <= now || row.EntryAttempts >= maxEntryAttempts).ToList();
        foreach (var row in deleted)
            transaction.Write(rows, row.Email, null);

        return Task.FromResult(deleted.Count);
    }

    private sealed record Row(Email Email, CodeHash CodeHash, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, int EntryAttempts)
    {
        public static Row From(OneTimeCode code) =>
            new(code.Email, code.CodeHash, code.IssuedAt, code.ExpiresAt, code.EntryAttempts);

        public OneTimeCode ToOneTimeCode() => OneTimeCode.FromStorage(Email, CodeHash, IssuedAt, ExpiresAt, EntryAttempts);
    }
}
