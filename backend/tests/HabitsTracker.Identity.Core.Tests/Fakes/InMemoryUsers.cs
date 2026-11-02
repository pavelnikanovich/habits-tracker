namespace HabitsTracker.Identity.Core.Tests.Fakes;

// IUsers in memory, as EfUsers behaves. It keeps a user's fields, not the User, and builds a new User on every
// read, so a change to a returned user reaches the store only through SaveAsync.
public sealed class InMemoryUsers : IUsers
{
    private readonly Dictionary<Guid, Row> rows = [];
    private readonly InMemoryTransaction transaction;
    private readonly CallLog? calls;

    // calls: null when a test does not check the order of calls.
    public InMemoryUsers(InMemoryTransaction transaction, CallLog? calls = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        this.transaction = transaction;
        this.calls = calls;
    }

    public Task<User?> FindByEmailAsync(Email email, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(email);
        return Task.FromResult(Find(email));
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        calls?.Record(this);
        return Task.FromResult(rows.TryGetValue(id, out var row) ? row.ToUser() : null);
    }

    // One user per email, as the unique index of identity.users; a repeated id fails, as its primary key.
    public Task<User> AddOrGetAsync(User user, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(user);

        if (Find(user.Email) is { } stored)
            return Task.FromResult(stored);

        var row = Row.From(user);
        if (rows.ContainsKey(row.Id))
            throw new InvalidOperationException("A user with this id is stored already.");

        transaction.Write(rows, row.Id, row);
        return Task.FromResult(row.ToUser());
    }

    // Only the locale changes, as in EfUsers; a user that is not stored stays not stored.
    public Task SaveAsync(User user, CancellationToken ct)
    {
        calls?.Record(this);
        ArgumentNullException.ThrowIfNull(user);

        if (rows.TryGetValue(user.Id, out var row))
            transaction.Write(rows, user.Id, row with { Locale = user.Locale });

        return Task.CompletedTask;
    }

    private User? Find(Email email) =>
        rows.Values.SingleOrDefault(row => row.Email == email)?.ToUser();

    // Value objects are immutable, so the row keeps them; only the aggregate is built anew.
    private sealed record Row(Guid Id, Email Email, TimeZone TimeZone, Locale Locale, DateTimeOffset CreatedAt)
    {
        public static Row From(User user) => new(user.Id, user.Email, user.TimeZone, user.Locale, user.CreatedAt);

        public User ToUser() => User.FromStorage(Id, Email, TimeZone, Locale, CreatedAt);
    }
}
