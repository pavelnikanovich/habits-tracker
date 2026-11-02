namespace HabitsTracker.Identity.Core;

public interface ICodeRequests
{
    // Transaction-scoped; InvalidOperationException outside a transaction.
    Task LockAsync(Email email, IpAddressPrefix ipAddressPrefix, CancellationToken ct);

    // Counts the requests with RequestedAt > windowStart.
    Task<CodeRequestCounts> CountAsync(
        Email email, IpAddressPrefix ipAddressPrefix, DateTimeOffset windowStart, CancellationToken ct);

    Task AddAsync(CodeRequest request, CancellationToken ct);

    Task<int> DeleteRequestedAtOrBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);
}
