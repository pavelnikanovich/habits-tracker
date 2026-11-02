namespace HabitsTracker.Identity.Core;

public interface ISessions
{
    Task<Session?> FindAsync(byte[] id, CancellationToken ct);

    Task AddAsync(Session session, CancellationToken ct);

    // Updates an existing session and never inserts one, so a session deleted meanwhile stays deleted.
    Task SaveAsync(Session session, CancellationToken ct);

    Task DeleteAsync(byte[] id, CancellationToken ct);

    Task DeleteAllOfUserAsync(Guid userId, CancellationToken ct);

    Task<int> DeleteInactiveAtOrBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);
}
