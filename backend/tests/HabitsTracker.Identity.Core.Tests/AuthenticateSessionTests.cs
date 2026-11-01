using HabitsTracker.Identity.Core.Tests.Fakes;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.Extensions.Time.Testing;

namespace HabitsTracker.Identity.Core.Tests;

public class AuthenticateSessionTests
{
    private static readonly DateTimeOffset L = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Email Ann = Email.Create("ann@example.com");

    private readonly FakeTimeProvider time = new(L);
    private readonly InMemoryUsers users;
    private readonly InMemorySessions sessions;

    public AuthenticateSessionTests()
    {
        var transaction = new InMemoryTransaction();
        users = new InMemoryUsers(transaction);
        sessions = new InMemorySessions(transaction);
    }

    [Fact]
    public async Task Activity_extends_the_session()
    {
        var (user, _, token) = await SignedInAtLAsync();
        time.Advance(TimeSpan.FromDays(29));

        var first = await AuthenticateAsync(token);

        Assert.NotNull(first);
        Assert.Equal(user.Id, first.User.Id);
        Assert.Equal(L.AddDays(29), first.Session.LastActivityAt);
        Assert.Equal(L.AddDays(29), (await sessions.FindAsync(first.Session.Id, Ct))?.LastActivityAt);

        time.Advance(TimeSpan.FromDays(29));

        var second = await AuthenticateAsync(token);

        Assert.NotNull(second);
        Assert.Equal(L.AddDays(58), second.Session.LastActivityAt);
    }

    [Fact]
    public async Task An_expired_session_is_not_signed_in()
    {
        var (_, session, token) = await SignedInAtLAsync();
        time.Advance(TimeSpan.FromDays(30));

        Assert.Null(await AuthenticateAsync(token));

        Assert.Equal(L, (await sessions.FindAsync(session.Id, Ct))?.LastActivityAt);
    }

    [Fact]
    public async Task An_unknown_token_is_not_signed_in()
    {
        await SignedInAtLAsync();

        Assert.Null(await AuthenticateAsync(SessionToken.Generate()));
    }

    // Users are never deleted in this epic and identity.sessions refers to identity.users, so a session always has
    // its user; the fakes have no such key, which lets this test break the rule.
    [Fact]
    public async Task A_session_without_its_user_fails()
    {
        var (session, token) = Session.Open(Guid.CreateVersion7(L), false, L);
        await sessions.AddAsync(session, Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AuthenticateAsync(token));

        Assert.Equal(L, (await sessions.FindAsync(session.Id, Ct))?.LastActivityAt);
    }

    [Fact]
    public async Task Gets_the_current_user()
    {
        var (user, _, _) = await SignedInAtLAsync();

        var current = await new GetCurrentUser(users).ExecuteAsync(user.Id, Ct);

        Assert.Equal(user.Id, current.Id);
        Assert.Equal(Ann, current.Email);
    }

    [Fact]
    public async Task A_missing_current_user_fails() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GetCurrentUser(users).ExecuteAsync(Guid.CreateVersion7(L), Ct));

    [Fact]
    public async Task Unset_arguments_are_programming_errors()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => AuthenticateAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => new GetCurrentUser(users).ExecuteAsync(Guid.Empty, Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<AuthenticatedSession?> AuthenticateAsync(SessionToken token) =>
        new AuthenticateSession(sessions, users, time).ExecuteAsync(token, Ct);

    private async Task<(User User, Session Session, SessionToken Token)> SignedInAtLAsync()
    {
        var user = await users.AddOrGetAsync(
            User.SignUp(Ann, TimeZone.FromBrowser("Europe/Warsaw"), Locale.FromStorage("pl"), L.AddDays(-1)), Ct);
        var (session, token) = Session.Open(user.Id, false, L);
        await sessions.AddAsync(session, Ct);
        return (user, session, token);
    }
}
