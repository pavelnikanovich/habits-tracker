namespace HabitsTracker.Identity.Core.Tests.Fakes;

// ICodeRequests in memory, as EfCodeRequests behaves. No method of the port returns a CodeRequest, so a row keeps
// only what counting and deleting read. The key is an id given in order, as the identity column of
// identity.code_requests gives it; like a sequence, it is not given back on a rollback.
public sealed class InMemoryCodeRequests : ICodeRequests
{
    private readonly Dictionary<long, Row> rows = [];
    private long lastId;
    private readonly InMemoryTransaction transaction;
    private readonly CallLog? calls;

    // calls: null when a test does not check the order of calls.
    public InMemoryCodeRequests(InMemoryTransaction transaction, CallLog? calls = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        this.transaction = transaction;
        this.calls = calls;
    }

    // Takes no lock: the use case tests make one call at a time, and the advisory locks are tested in the storage
    // adapter's concurrency tests. Outside a transaction it fails, as EfCodeRequests does.
    public Task LockAsync(Email email, IpAddressPrefix ipAddressPrefix, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(ipAddressPrefix);

        if (!transaction.InProgress)
            throw new InvalidOperationException("Code request locks are taken inside a transaction.");

        return Task.CompletedTask;
    }

    public Task<CodeRequestCounts> CountAsync(
        Email email, IpAddressPrefix ipAddressPrefix, DateTimeOffset windowStart, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(ipAddressPrefix);

        var inWindow = rows.Values.Where(row => row.RequestedAt > windowStart).ToList();
        var forEmail = inWindow.Where(row => row.Email == email).ToList();
        var forPrefix = inWindow.Where(row => row.IpAddressPrefix == ipAddressPrefix).ToList();

        // Min over nullable moments is null for no rows, as SQL's MIN.
        return Task.FromResult(new CodeRequestCounts(
            forEmail.Count,
            forEmail.Min(row => (DateTimeOffset?)row.RequestedAt),
            forPrefix.Count,
            forPrefix.Min(row => (DateTimeOffset?)row.RequestedAt)));
    }

    public Task AddAsync(CodeRequest request, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(request);
        transaction.Write(rows, ++lastId, new Row(request.Email, request.IpAddressPrefix, request.RequestedAt));
        return Task.CompletedTask;
    }

    public Task<int> DeleteRequestedAtOrBeforeAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        calls?.Record(this);
        var deleted = rows.Where(pair => pair.Value.RequestedAt <= cutoff).Select(pair => pair.Key).ToList();
        foreach (var id in deleted)
            transaction.Write(rows, id, null);

        return Task.FromResult(deleted.Count);
    }

    private sealed record Row(Email Email, IpAddressPrefix IpAddressPrefix, DateTimeOffset RequestedAt);
}
