using HabitsTracker.Identity.Core.Tests.Fakes;
using HabitsTracker.Identity.Core.UseCases;

namespace HabitsTracker.Identity.Core.Tests;

public class ChangeLocaleTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Locale Polish = Locale.FromStorage("pl");

    private readonly CallLog calls = new();
    private readonly InMemoryUsers users;

    public ChangeLocaleTests() => users = new InMemoryUsers(new InMemoryTransaction(), calls);

    [Fact]
    public async Task Changes_only_this_users_locale()
    {
        var ann = await SignUpAsync("ann@example.com");
        var bob = await SignUpAsync("bob@example.com");

        await ChangeAsync(ann.Id, "ru");

        Assert.Equal(Locale.FromStorage("ru"), (await users.FindByIdAsync(ann.Id, Ct))?.Locale);
        Assert.Equal(Polish, (await users.FindByIdAsync(bob.Id, Ct))?.Locale);
    }

    // M4: the locale is built before the user is read, as in RequestCode and SignIn.
    [Fact]
    public async Task An_unavailable_locale_is_refused_before_the_user_is_read()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => ChangeAsync(Guid.CreateVersion7(T), "de"));

        Assert.Equal(DomainError.InvalidLocale, exception.Error);
        Assert.Empty(calls.Entries);
    }

    // Users are never deleted in this epic, and the id comes from an authenticated session.
    [Fact]
    public async Task A_missing_user_fails() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => ChangeAsync(Guid.CreateVersion7(T), "ru"));

    [Fact]
    public async Task Unset_arguments_are_programming_errors() =>
        await Assert.ThrowsAsync<ArgumentException>(() => ChangeAsync(Guid.Empty, "ru"));

    [Fact]
    public void Unset_dependencies_are_programming_errors()
    {
        Assert.Throws<ArgumentNullException>("users", () => new ChangeLocale(null!, new FixedLocales()));
        Assert.Throws<ArgumentNullException>("locales", () => new ChangeLocale(users, null!));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task ChangeAsync(Guid userId, string? localeCode) =>
        new ChangeLocale(users, new FixedLocales()).ExecuteAsync(userId, localeCode, Ct);

    private Task<User> SignUpAsync(string email) =>
        users.AddOrGetAsync(User.SignUp(Email.Create(email), TimeZone.FromBrowser("Europe/Warsaw"), Polish, T), Ct);
}
