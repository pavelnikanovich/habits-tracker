namespace HabitsTracker.Identity.Core;

// Aggregate: one counted request for a code, immutable once created (FR-004).
public sealed class CodeRequest
{
    private CodeRequest(long id, Email email, IpAddressPrefix ipAddressPrefix, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(ipAddressPrefix);
        Instant.Require(requestedAt);

        Id = id;
        Email = email;
        IpAddressPrefix = ipAddressPrefix;
        RequestedAt = requestedAt;
    }

    // 0 until stored: the database generates it.
    public long Id { get; private set; }

    public Email Email { get; private set; }

    public IpAddressPrefix IpAddressPrefix { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public static CodeRequest Create(Email email, IpAddressPrefix ipAddressPrefix, DateTimeOffset now) =>
        new(0, email, ipAddressPrefix, now);

    // Checked here, not in the constructor as in the other aggregates: Create passes 0 for a request not yet
    // stored, and only a stored request has an id, which the database generates from 1.
    public static CodeRequest FromStorage(long id, Email email, IpAddressPrefix ipAddressPrefix, DateTimeOffset requestedAt)
    {
        if (id <= 0)
            throw new DomainException(DomainError.CorruptRecord, "A stored code request has no id.");

        return new CodeRequest(id, email, ipAddressPrefix, requestedAt);
    }
}
