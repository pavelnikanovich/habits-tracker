namespace HabitsTracker.Identity.Core;

// Aggregate: the code last sent to an email, stored as its hash.
public sealed class OneTimeCode
{
    public const int MaxEntryAttempts = 5;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    // Every factory goes through here, so the checks hold for all of them. Only loading can break an
    // invariant in this epic, hence CorruptRecord.
    private OneTimeCode(Email email, CodeHash codeHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt, int entryAttempts)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(codeHash);
        Instant.Require(issuedAt);
        Instant.Require(expiresAt);

        if (expiresAt != issuedAt + Lifetime)
            throw new DomainException(DomainError.CorruptRecord, "A one-time code does not expire one lifetime after it was issued.");

        if (entryAttempts is < 0 or > MaxEntryAttempts)
            throw new DomainException(DomainError.CorruptRecord, "A one-time code has entry attempts out of range.");

        Email = email;
        CodeHash = codeHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        EntryAttempts = entryAttempts;
    }

    public Email Email { get; private set; }

    public CodeHash CodeHash { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int EntryAttempts { get; private set; }

    // In memory only: IOneTimeCodes.SaveAsync deletes a used code instead of storing the flag.
    public bool IsUsed { get; private set; }

    public static OneTimeCode Issue(Email email, CodeHash codeHash, DateTimeOffset now) =>
        new(email, codeHash, now, now + Lifetime, entryAttempts: 0);

    public static OneTimeCode FromStorage(
        Email email, CodeHash codeHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt, int entryAttempts) =>
        new(email, codeHash, issuedAt, expiresAt, entryAttempts);

    // Declared for the use cases; Task 11's tests specify it.
    public CodeVerification Verify(CodeHash entered, DateTimeOffset now) => throw new NotImplementedException();
}
