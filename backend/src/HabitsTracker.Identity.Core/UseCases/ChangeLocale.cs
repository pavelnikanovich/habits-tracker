namespace HabitsTracker.Identity.Core.UseCases;

// Change the signed-in user's locale (FR-023). No transaction: it changes one user with one write, and the last
// change wins (data-model.md, "Concurrent changes").
public sealed class ChangeLocale
{
    private readonly IUsers users;
    private readonly ILocales locales;

    public ChangeLocale(IUsers users, ILocales locales)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(locales);

        this.users = users;
        this.locales = locales;
    }

    public async Task ExecuteAsync(Guid userId, string? localeCode, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("The user id is not set.", nameof(userId));

        // Built before the user is read (M4), as in RequestCode and SignIn.
        var locale = Locale.Create(localeCode, locales);

        var user = await users.FindByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("The signed-in user is not stored; users are never deleted in this epic.");

        user.ChangeLocale(locale);
        await users.SaveAsync(user, ct);
    }
}
