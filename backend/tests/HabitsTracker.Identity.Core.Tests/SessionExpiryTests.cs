namespace HabitsTracker.Identity.Core.Tests;

public class SessionExpiryTests
{
    private static readonly DateTimeOffset L = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private const int Day = 24 * 60 * 60;

    [Theory]
    [InlineData(29 * Day, false)]
    [InlineData(30 * Day - 1, false)]
    [InlineData(30 * Day, true)]
    public void Expires_after_30_days_without_activity(int secondsAfterActivity, bool expired)
    {
        var session = ActiveAt(L);

        Assert.Equal(expired, session.IsExpired(L.AddSeconds(secondsAfterActivity)));
    }

    [Fact]
    public void Activity_moves_the_last_activity()
    {
        var session = ActiveAt(L);

        session.RecordActivity(L.AddDays(29));

        Assert.Equal(L.AddDays(29), session.LastActivityAt);
        Assert.False(session.IsExpired(L.AddDays(30)));
    }

    [Fact]
    public void Activity_never_moves_the_last_activity_back()
    {
        // A clock stepped back must not store a last activity before the creation, which loading refuses.
        var session = ActiveAt(L);

        session.RecordActivity(L.AddDays(-2));

        Assert.Equal(L, session.LastActivityAt);
    }

    [Fact]
    public void Activity_on_an_expired_session_is_refused()
    {
        var session = ActiveAt(L);

        Assert.Throws<InvalidOperationException>(() => session.RecordActivity(L.AddDays(30)));

        Assert.Equal(L, session.LastActivityAt);
    }

    [Fact]
    public void Unset_arguments_are_programming_errors()
    {
        var session = ActiveAt(L);
        var notUtc = new DateTimeOffset(2026, 11, 2, 11, 0, 0, TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(() => session.IsExpired(default));
        Assert.Throws<ArgumentException>(() => session.IsExpired(notUtc));
        Assert.Throws<ArgumentException>(() => session.RecordActivity(default));
        Assert.Throws<ArgumentException>(() => session.RecordActivity(notUtc));
    }

    // Created a day before its last activity, so the tests tell the last activity from the creation.
    private static Session ActiveAt(DateTimeOffset lastActivityAt) =>
        Session.FromStorage(new byte[32], Guid.CreateVersion7(lastActivityAt), false, lastActivityAt.AddDays(-1), lastActivityAt);
}
