using HabitsTracker.Identity.Core.Tests.Fakes;
using HabitsTracker.Identity.Core.UseCases;

namespace HabitsTracker.Identity.Core.Tests;

public class SignOutTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ann = Guid.CreateVersion7(T);
    private static readonly Guid Bob = Guid.CreateVersion7(T.AddSeconds(1));

    private readonly InMemorySessions sessions = new(new InMemoryTransaction());

    [Fact]
    public async Task Sign_out_deletes_only_this_session()
    {
        var current = await OpenAsync(Ann);
        var annsOther = await OpenAsync(Ann);
        var bobs = await OpenAsync(Bob);

        await new SignOut(sessions).ExecuteAsync(current.Id, Ct);

        Assert.Null(await sessions.FindAsync(current.Id, Ct));
        Assert.NotNull(await sessions.FindAsync(annsOther.Id, Ct));
        Assert.NotNull(await sessions.FindAsync(bobs.Id, Ct));
    }

    [Fact]
    public async Task Sign_out_unset_arguments_are_programming_errors()
    {
        Assert.Throws<ArgumentNullException>("sessions", () => new SignOut(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => new SignOut(sessions).ExecuteAsync(null!, Ct));
    }

    [Fact]
    public async Task Sign_out_everywhere_deletes_every_session_of_the_user_and_no_other()
    {
        var current = await OpenAsync(Ann);
        var annsOther = await OpenAsync(Ann);
        var bobs = await OpenAsync(Bob);

        await new SignOutEverywhere(sessions).ExecuteAsync(Ann, Ct);

        Assert.Null(await sessions.FindAsync(current.Id, Ct));
        Assert.Null(await sessions.FindAsync(annsOther.Id, Ct));
        Assert.NotNull(await sessions.FindAsync(bobs.Id, Ct));
    }

    [Fact]
    public async Task Sign_out_everywhere_unset_arguments_are_programming_errors()
    {
        Assert.Throws<ArgumentNullException>("sessions", () => new SignOutEverywhere(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => new SignOutEverywhere(sessions).ExecuteAsync(Guid.Empty, Ct));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Session> OpenAsync(Guid userId)
    {
        var (session, _) = Session.Open(userId, false, T);
        await sessions.AddAsync(session, Ct);
        return session;
    }
}
