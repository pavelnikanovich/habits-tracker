using HabitsTracker.Identity.Core;
using TimeZone = HabitsTracker.Identity.Core.TimeZone;

namespace HabitsTracker.Tests.PortContracts;

// The contract of IUsers (data-model.md, "Driven ports"): the PostgreSQL store and the in-memory fake pass it.
public abstract class UsersContract
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email Ada = Email.Create("ada@example.com");

    protected abstract IUsers Users { get; }

    [Theory]
    [InlineData("Europe/Warsaw")]
    [InlineData(null)] // Not recognized: UTC with Recognized == false.
    public async Task User_round_trips(string? browserTimeZone)
    {
        var ct = TestContext.Current.CancellationToken;
        var user = User.SignUp(Ada, TimeZone.FromBrowser(browserTimeZone), Locale.FromStorage("pl"), Now);

        AssertStored(user, await Users.AddOrGetAsync(user, ct));

        AssertStored(user, await Users.FindByEmailAsync(Ada, ct));
        AssertStored(user, await Users.FindByIdAsync(user.Id, ct));
        Assert.Null(await Users.FindByEmailAsync(Email.Create("bob@example.com"), ct));
    }

    [Fact]
    public async Task Add_or_get_returns_the_stored_user_for_the_same_email()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = User.SignUp(Ada, TimeZone.FromBrowser("Europe/Warsaw"), Locale.FromStorage("pl"), Now);
        var second = User.SignUp(Ada, TimeZone.FromBrowser(null), Locale.FromStorage("en"), Now.AddMinutes(1));
        await Users.AddOrGetAsync(first, ct);

        var result = await Users.AddOrGetAsync(second, ct);

        AssertStored(first, result);
        AssertStored(first, await Users.FindByEmailAsync(Ada, ct));
    }

    [Fact]
    public async Task Saving_stores_the_changed_locale()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = User.SignUp(Ada, TimeZone.FromBrowser("Europe/Warsaw"), Locale.FromStorage("pl"), Now);
        await Users.AddOrGetAsync(user, ct);

        user.ChangeLocale(Locale.FromStorage("ru"));
        await Users.SaveAsync(user, ct);

        AssertStored(user, await Users.FindByIdAsync(user.Id, ct));
    }

    private static void AssertStored(User expected, User? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Email, actual.Email);
        Assert.Equal(expected.TimeZone, actual.TimeZone);
        Assert.Equal(expected.Locale, actual.Locale);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
    }
}
