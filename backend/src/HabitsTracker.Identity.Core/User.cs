namespace HabitsTracker.Identity.Core;

// Aggregate: a person known by an email address.
public sealed class User
{
    // Every factory goes through here, so the checks hold for all of them. Only loading can break an
    // invariant in this epic, hence CorruptRecord.
    private User(Guid id, Email email, TimeZone timeZone, Locale locale, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(locale);
        Instant.Require(createdAt);

        if (id == Guid.Empty)
            throw new DomainException(DomainError.CorruptRecord, "A user has an empty id.");

        Id = id;
        Email = email;
        TimeZone = timeZone;
        Locale = locale;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Email Email { get; private set; }

    public TimeZone TimeZone { get; private set; }

    public Locale Locale { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Version 7 because it is ordered by time: new users go to the end of the primary key index, which stays
    // compact (data-model.md, agent's call). Its first 48 bits show the sign-up time to the millisecond; the API
    // neither takes nor returns a user id (contracts/http-api.md), so that time stays on the server. Revisit if
    // an id is ever shown outside the server.
    public static User SignUp(Email email, TimeZone timeZone, Locale locale, DateTimeOffset now)
    {
        // Before CreateVersion7, which takes any offset and refuses only instants before 1970, with its own exception.
        Instant.Require(now);

        return new User(Guid.CreateVersion7(now), email, timeZone, locale, now);
    }

    public static User FromStorage(Guid id, Email email, TimeZone timeZone, Locale locale, DateTimeOffset createdAt) =>
        new(id, email, timeZone, locale, createdAt);

    public void ChangeLocale(Locale locale)
    {
        ArgumentNullException.ThrowIfNull(locale);

        Locale = locale;
    }
}
