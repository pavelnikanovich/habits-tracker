namespace HabitsTracker.Identity.Core.UseCases;

// The retention cleanup (FR-033; data-model.md, "Retention"): codes expired or exhausted, code requests past the
// window, sessions past the idle timeout. The core computes every condition and the stores only apply it
// ("Deleting past the aggregates"). No transaction: the three deletes are independent and idempotent, and a
// failed one is finished by the next run.
public sealed class DeleteExpiredRecords
{
    private readonly IOneTimeCodes oneTimeCodes;
    private readonly ICodeRequests codeRequests;
    private readonly ISessions sessions;
    private readonly TimeProvider time;
    private readonly IdentityContextOptions options;

    public DeleteExpiredRecords(
        IOneTimeCodes oneTimeCodes,
        ICodeRequests codeRequests,
        ISessions sessions,
        TimeProvider time,
        IdentityContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(oneTimeCodes);
        ArgumentNullException.ThrowIfNull(codeRequests);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);

        this.oneTimeCodes = oneTimeCodes;
        this.codeRequests = codeRequests;
        this.sessions = sessions;
        this.time = time;
        this.options = options;
    }

    public async Task<DeletedRecords> ExecuteAsync(CancellationToken ct)
    {
        // One moment for all three conditions.
        var now = time.GetUtcNow();

        var codes = await oneTimeCodes.DeleteExpiredOrExhaustedAsync(now, OneTimeCode.MaxEntryAttempts, ct);
        var requests = await codeRequests.DeleteRequestedAtOrBeforeAsync(now - options.CodeRequestLimits.Window, ct);
        var inactive = await sessions.DeleteInactiveAtOrBeforeAsync(now - Session.IdleTimeout, ct);

        return new DeletedRecords(codes, requests, inactive);
    }
}
