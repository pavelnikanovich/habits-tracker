using HabitsTracker.Identity.Core;
using TimeZone = HabitsTracker.Identity.Core.TimeZone;

namespace HabitsTracker.Tests.PortContracts;

// The contract of ISessions (data-model.md, "Driven ports"): the PostgreSQL store and the in-memory fake pass it.
// Every session belongs to a stored user, as in the application.
public abstract class SessionsContract
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    protected abstract IUsers Users { get; }

    protected abstract ISessions Sessions { get; }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Session_round_trips(bool remembered)
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = await AddUserAsync("ada@example.com", ct);
        var session = NewSession(userId, Now - TimeSpan.FromHours(1), Now, remembered);

        await Sessions.AddAsync(session, ct);

        AssertStored(session, await Sessions.FindAsync(session.Id, ct));
        Assert.Null(await Sessions.FindAsync(SessionToken.Generate().ComputeId(), ct));
    }

    [Fact]
    public async Task Saving_stores_the_last_activity()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = await AddUserAsync("ada@example.com", ct);
        var session = NewSession(userId, Now - TimeSpan.FromHours(1), Now - TimeSpan.FromHours(1));
        await Sessions.AddAsync(session, ct);
        var active = Session.FromStorage(session.Id, userId, session.Remembered, session.CreatedAt, Now);

        await Sessions.SaveAsync(active, ct);

        AssertStored(active, await Sessions.FindAsync(session.Id, ct));
    }

    [Fact]
    public async Task Saving_a_deleted_session_does_not_restore_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = await AddUserAsync("ada@example.com", ct);
        var session = NewSession(userId, Now - TimeSpan.FromHours(1), Now - TimeSpan.FromHours(1));
        await Sessions.AddAsync(session, ct);
        await Sessions.DeleteAsync(session.Id, ct);

        await Sessions.SaveAsync(Session.FromStorage(session.Id, userId, session.Remembered, session.CreatedAt, Now), ct);

        Assert.Null(await Sessions.FindAsync(session.Id, ct));
    }

    [Fact]
    public async Task Delete_all_of_user_leaves_other_users_sessions()
    {
        var ct = TestContext.Current.CancellationToken;
        var adaId = await AddUserAsync("ada@example.com", ct);
        var bobId = await AddUserAsync("bob@example.com", ct);
        var adaPhone = NewSession(adaId, Now, Now);
        var adaLaptop = NewSession(adaId, Now, Now);
        var bobPhone = NewSession(bobId, Now, Now);
        foreach (var session in new[] { adaPhone, adaLaptop, bobPhone })
            await Sessions.AddAsync(session, ct);

        await Sessions.DeleteAllOfUserAsync(adaId, ct);

        Assert.Null(await Sessions.FindAsync(adaPhone.Id, ct));
        Assert.Null(await Sessions.FindAsync(adaLaptop.Id, ct));
        AssertStored(bobPhone, await Sessions.FindAsync(bobPhone.Id, ct));
    }

    [Fact]
    public async Task Delete_inactive_at_or_before()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = await AddUserAsync("ada@example.com", ct);
        var cutoff = Now - Session.IdleTimeout;
        var inactive = NewSession(userId, cutoff - TimeSpan.FromHours(1), cutoff);
        var active = NewSession(userId, cutoff - TimeSpan.FromHours(1), cutoff + TimeSpan.FromSeconds(1));
        await Sessions.AddAsync(inactive, ct);
        await Sessions.AddAsync(active, ct);

        Assert.Equal(1, await Sessions.DeleteInactiveAtOrBeforeAsync(cutoff, ct));

        Assert.Null(await Sessions.FindAsync(inactive.Id, ct));
        AssertStored(active, await Sessions.FindAsync(active.Id, ct));
    }

    private async Task<Guid> AddUserAsync(string email, CancellationToken ct)
    {
        var user = User.SignUp(Email.Create(email), TimeZone.FromBrowser("Europe/Warsaw"), Locale.FromStorage("en"), Now - TimeSpan.FromDays(60));
        await Users.AddOrGetAsync(user, ct);
        return user.Id;
    }

    private static Session NewSession(Guid userId, DateTimeOffset createdAt, DateTimeOffset lastActivityAt, bool remembered = false) =>
        Session.FromStorage(SessionToken.Generate().ComputeId(), userId, remembered, createdAt, lastActivityAt);

    private static void AssertStored(Session expected, Session? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.Equal(expected.Remembered, actual.Remembered);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.LastActivityAt, actual.LastActivityAt);
    }
}
