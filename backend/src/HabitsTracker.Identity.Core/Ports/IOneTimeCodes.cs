namespace HabitsTracker.Identity.Core;

public interface IOneTimeCodes
{
    // Exclusive access to the code until the transaction ends, so concurrent entries are counted one after another.
    Task<OneTimeCode?> FindExclusiveAsync(Email email, CancellationToken ct);

    Task ReplaceAsync(OneTimeCode code, CancellationToken ct);

    // Deletes a used code, else stores its entry attempts.
    Task SaveAsync(OneTimeCode code, CancellationToken ct);

    // Deletes the codes with ExpiresAt <= now or EntryAttempts >= maxEntryAttempts.
    Task<int> DeleteExpiredOrExhaustedAsync(DateTimeOffset now, int maxEntryAttempts, CancellationToken ct);
}
