namespace HabitsTracker.Identity.Core;

public interface IOneTimeCodes
{
    // Exclusive access to the code until the transaction ends, so concurrent entries are counted one after another.
    Task<OneTimeCode?> FindExclusiveAsync(Email email, CancellationToken ct);

    Task ReplaceAsync(OneTimeCode code, CancellationToken ct);

    // Deletes a used code, else stores its entry attempts. Call it only for a code read with FindExclusiveAsync in
    // the same transaction: the stored code is found by email alone, and only that exclusive access keeps a code
    // requested meanwhile from being the one this call deletes or changes.
    Task SaveAsync(OneTimeCode code, CancellationToken ct);

    // Deletes the codes with ExpiresAt <= now or EntryAttempts >= maxEntryAttempts.
    Task<int> DeleteExpiredOrExhaustedAsync(DateTimeOffset now, int maxEntryAttempts, CancellationToken ct);
}
