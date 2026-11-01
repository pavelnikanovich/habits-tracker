namespace HabitsTracker.Identity.Core;

public interface IIdentityTransaction
{
    // Commits when work returns, rolls back when it throws.
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
