namespace HabitsTracker.Identity.Core.UseCases;

// Sign out (FR-015): this browser's session is revoked, the user's other sessions stay. No transaction: one delete,
// and a request extending the session at the same moment cannot bring it back, since SaveAsync never inserts
// (data-model.md, "Concurrent changes").
public sealed class SignOut
{
    private readonly ISessions sessions;

    public SignOut(ISessions sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        this.sessions = sessions;
    }

    public Task ExecuteAsync(byte[] sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        if (sessionId.Length != Session.IdLength)
            throw new ArgumentException("The session id is not 32 bytes.", nameof(sessionId));

        return sessions.DeleteAsync(sessionId, ct);
    }
}
