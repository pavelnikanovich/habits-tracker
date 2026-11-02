namespace HabitsTracker.Identity.Core.UseCases;

// Sign out everywhere (FR-015): every session of the user is revoked at once (data-model.md, "Transactions that
// change more than one aggregate"). One delete by the user id, without loading the sessions ("Deleting past the
// aggregates"); a single statement revokes them together, so no explicit transaction is taken — revisit if the use
// case gains a second step.
public sealed class SignOutEverywhere
{
    private readonly ISessions sessions;

    public SignOutEverywhere(ISessions sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        this.sessions = sessions;
    }

    public Task ExecuteAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("The user id is not set.", nameof(userId));

        return sessions.DeleteAllOfUserAsync(userId, ct);
    }
}
