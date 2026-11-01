namespace HabitsTracker.Identity.Core.Tests.Fakes;

// ISessions in memory, as EfSessions behaves: a new Session on every read, and SaveAsync updates and never inserts.
// The key is the id in hex, since a byte[] key compares by reference; the id is copied in and out, so no caller
// shares the stored array.
public sealed class InMemorySessions : ISessions
{
    private readonly Dictionary<string, Row> rows = new(StringComparer.Ordinal);
    private readonly InMemoryTransaction transaction;
    private readonly CallLog? calls;

    // calls: null when a test does not check the order of calls.
    public InMemorySessions(InMemoryTransaction transaction, CallLog? calls = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        this.transaction = transaction;
        this.calls = calls;
    }

    public Task<Session?> FindAsync(byte[] id, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(id);
        return Task.FromResult(rows.TryGetValue(Key(id), out var row) ? row.ToSession() : null);
    }

    // A repeated id fails, as the primary key of identity.sessions.
    public Task AddAsync(Session session, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(session);

        var key = Key(session.Id);
        if (rows.ContainsKey(key))
            throw new InvalidOperationException("A session with this id is stored already.");

        transaction.Write(rows, key, Row.From(session));
        return Task.CompletedTask;
    }

    // The last activity is the only state a session changes; a session deleted meanwhile stays deleted.
    public Task SaveAsync(Session session, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(session);

        var key = Key(session.Id);
        if (rows.TryGetValue(key, out var row))
            transaction.Write(rows, key, row with { LastActivityAt = session.LastActivityAt });

        return Task.CompletedTask;
    }

    public Task DeleteAsync(byte[] id, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(id);
        transaction.Write(rows, Key(id), null);
        return Task.CompletedTask;
    }

    public Task DeleteAllOfUserAsync(Guid userId, CancellationToken ct)
    {
        calls?.Record(this);
        Delete(row => row.UserId == userId);
        return Task.CompletedTask;
    }

    public Task<int> DeleteInactiveAtOrBeforeAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        calls?.Record(this);
        return Task.FromResult(Delete(row => row.LastActivityAt <= cutoff));
    }

    private int Delete(Func<Row, bool> match)
    {
        var keys = rows.Where(pair => match(pair.Value)).Select(pair => pair.Key).ToList();
        foreach (var key in keys)
            transaction.Write(rows, key, null);

        return keys.Count;
    }

    private static string Key(byte[] id) => Convert.ToHexString(id);

    private sealed record Row(byte[] Id, Guid UserId, bool Remembered, DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt)
    {
        public static Row From(Session session) =>
            new(session.Id.ToArray(), session.UserId, session.Remembered, session.CreatedAt, session.LastActivityAt);

        public Session ToSession() => Session.FromStorage(Id.ToArray(), UserId, Remembered, CreatedAt, LastActivityAt);
    }
}
