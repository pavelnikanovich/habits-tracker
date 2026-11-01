namespace HabitsTracker.Identity.Core.UseCases;

// Authenticate a session (FR-013): the session the cookie's token names, unless it has expired, extended to
// IdleTimeout from now. No transaction: it changes one session with one write, and the last write wins
// (data-model.md, "Concurrent changes"); SaveAsync never inserts, so a session signed out meanwhile stays out.
public sealed class AuthenticateSession
{
    private readonly ISessions sessions;
    private readonly IUsers users;
    private readonly TimeProvider time;

    public AuthenticateSession(ISessions sessions, IUsers users, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(time);

        this.sessions = sessions;
        this.users = users;
        this.time = time;
    }

    public async Task<AuthenticatedSession?> ExecuteAsync(SessionToken token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);

        var session = await sessions.FindAsync(token.ComputeId(), ct);
        var now = time.GetUtcNow();
        if (session is null || session.IsExpired(now))
            return null;

        // Read before the activity is stored, so a session without its user is not extended.
        var user = await users.FindByIdAsync(session.UserId, ct)
            ?? throw new InvalidOperationException("A session's user is not stored; users are never deleted in this epic.");

        session.RecordActivity(now);
        await sessions.SaveAsync(session, ct);

        return new AuthenticatedSession(user, session);
    }
}
