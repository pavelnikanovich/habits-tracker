namespace HabitsTracker.Identity.Core;

// Aggregate: a signed-in browser, known by the SHA-256 of the token in its cookie.
public sealed class Session
{
    private const int IdLength = 32;

    public static readonly TimeSpan IdleTimeout = TimeSpan.FromDays(30);

    // Every factory goes through here, so the checks hold for all of them. Only loading can break an
    // invariant in this epic, hence CorruptRecord.
    private Session(byte[] id, Guid userId, bool remembered, DateTimeOffset createdAt, DateTimeOffset lastActivityAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        Instant.Require(createdAt);
        Instant.Require(lastActivityAt);

        if (userId == Guid.Empty)
            throw new ArgumentException("The user id is not set.", nameof(userId));

        if (id.Length != IdLength)
            throw new DomainException(DomainError.CorruptRecord, "A session id is not 32 bytes.");

        if (lastActivityAt < createdAt)
            throw new DomainException(DomainError.CorruptRecord, "A session's last activity is before its creation.");

        Id = id;
        UserId = userId;
        Remembered = remembered;
        CreatedAt = createdAt;
        LastActivityAt = lastActivityAt;
    }

    // The model's byte[], neither copied nor wrapped, unlike CodeHash: the id is not a secret, Open gets a fresh
    // array from ComputeId and FromStorage one the storage adapter materialized, and code compares ids only with
    // SequenceEqual. Revisit if a caller ever changes an id array it holds.
    public byte[] Id { get; private set; }

    public Guid UserId { get; private set; }

    public bool Remembered { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public static (Session Session, SessionToken Token) Open(Guid userId, bool remembered, DateTimeOffset now)
    {
        var token = SessionToken.Generate();

        return (new Session(token.ComputeId(), userId, remembered, now, now), token);
    }

    public static Session FromStorage(
        byte[] id, Guid userId, bool remembered, DateTimeOffset createdAt, DateTimeOffset lastActivityAt) =>
        new(id, userId, remembered, createdAt, lastActivityAt);

    public bool IsExpired(DateTimeOffset now)
    {
        Instant.Require(now);

        return now >= LastActivityAt + IdleTimeout;
    }

    // Extends the session to IdleTimeout from now. A clock stepped back does not move the last activity back, so
    // a session never holds a last activity before its creation, which loading refuses as CorruptRecord.
    public void RecordActivity(DateTimeOffset now)
    {
        if (IsExpired(now))
            throw new InvalidOperationException("Activity is recorded only on a session that has not expired.");

        if (now > LastActivityAt)
            LastActivityAt = now;
    }
}
