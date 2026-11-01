namespace HabitsTracker.Identity.Core.Tests.Fakes;

// IIdentityTransaction in memory. The stores change their rows only through Write, which inside RunAsync remembers
// the row it replaced; when the work throws, those rows are put back newest first and the exception goes on.
// A write outside RunAsync is kept, as an autocommitted statement is.
public sealed class InMemoryTransaction : IIdentityTransaction
{
    private readonly CallLog? calls;
    private List<Action>? undo;

    // calls: null when a test does not check the order of calls.
    public InMemoryTransaction(CallLog? calls = null)
    {
        this.calls = calls;
    }

    public bool InProgress => undo is not null;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (undo is not null)
            throw new InvalidOperationException("A transaction is already in progress.");

        var actions = new List<Action>();
        undo = actions;
        calls?.Record(this, "Begin");
        try
        {
            var result = await work(ct);
            calls?.Record(this, "Commit");
            return result;
        }
        catch
        {
            for (var i = actions.Count - 1; i >= 0; i--)
                actions[i]();

            calls?.Record(this, "Rollback");
            throw;
        }
        finally
        {
            undo = null;
        }
    }

    // A null row deletes. The previous row, or its absence, is what a rollback restores.
    public void Write<TKey, TRow>(Dictionary<TKey, TRow> rows, TKey key, TRow? row)
        where TKey : notnull
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(rows);

        var existed = rows.TryGetValue(key, out var previous);
        if (row is null)
            rows.Remove(key);
        else
            rows[key] = row;

        undo?.Add(() =>
        {
            if (existed)
                rows[key] = previous!;
            else
                rows.Remove(key);
        });
    }
}
