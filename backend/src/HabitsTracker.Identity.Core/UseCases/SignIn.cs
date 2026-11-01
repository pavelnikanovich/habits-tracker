using System.Diagnostics;

namespace HabitsTracker.Identity.Core.UseCases;

// Sign in (data-model.md, "Transactions that change more than one aggregate"). Inside one transaction: take the
// code exclusively, verify the entry and store what it changed (a used code is deleted, a counted attempt kept);
// only on a correct entry find or create the user, open the session and revoke the session the browser held. A
// wrong entry returns a result rather than throwing, so its attempt is committed (FR-002).
public sealed class SignIn
{
    private readonly IIdentityTransaction transaction;
    private readonly IOneTimeCodes oneTimeCodes;
    private readonly IUsers users;
    private readonly ISessions sessions;
    private readonly ILocales locales;
    private readonly TimeProvider time;
    private readonly IdentityContextOptions options;

    public SignIn(
        IIdentityTransaction transaction,
        IOneTimeCodes oneTimeCodes,
        IUsers users,
        ISessions sessions,
        ILocales locales,
        TimeProvider time,
        IdentityContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(oneTimeCodes);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(locales);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);

        this.transaction = transaction;
        this.oneTimeCodes = oneTimeCodes;
        this.users = users;
        this.sessions = sessions;
        this.locales = locales;
        this.time = time;
        this.options = options;
    }

    public Task<SignInResult> ExecuteAsync(
        Email email,
        OneTimeCodeValue code,
        bool rememberMe,
        string? browserTimeZone,
        string? localeCode,
        SessionToken? heldSession,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(code);
        // Built before the transaction and on every path (M4): built only for a new user, an unavailable locale
        // would be refused for a new email and accepted for an existing one, telling which emails have a user.
        var locale = Locale.Create(localeCode, locales);

        return transaction.RunAsync(
            work => SignInAsync(email, code, rememberMe, browserTimeZone, locale, heldSession, work), ct);
    }

    private async Task<SignInResult> SignInAsync(
        Email email,
        OneTimeCodeValue code,
        bool rememberMe,
        string? browserTimeZone,
        Locale locale,
        SessionToken? heldSession,
        CancellationToken ct)
    {
        var stored = await oneTimeCodes.FindExclusiveAsync(email, ct);
        if (stored is null)
            return new SignInResult.NewCodeRequired();

        // Read once the code is held: an entry queued behind another entry of the same code waits for it, and the
        // 10 minutes are judged when this entry is counted.
        var now = time.GetUtcNow();
        var verification = stored.Verify(CodeHash.Compute(options.CodeHashKey.Span, email, code), now);
        await oneTimeCodes.SaveAsync(stored, ct);

        switch (verification)
        {
            case CodeVerification.SignIn:
                break;
            case CodeVerification.Incorrect:
                return new SignInResult.CodeIncorrect();
            case CodeVerification.NewCodeRequired:
                return new SignInResult.NewCodeRequired();
            default:
                throw new UnreachableException();
        }

        // The time zone and the locale are taken at the first sign-in only (FR-009); a later one keeps the user's.
        var user = await users.FindByEmailAsync(email, ct)
            ?? await users.AddOrGetAsync(User.SignUp(email, TimeZone.FromBrowser(browserTimeZone), locale, now), ct);

        var (session, token) = Session.Open(user.Id, rememberMe, now);
        await sessions.AddAsync(session, ct);

        // The session the browser held ends with this sign-in, whichever user it belonged to (research R19, S4).
        if (heldSession is not null && await sessions.FindAsync(heldSession.ComputeId(), ct) is { } held)
            await sessions.DeleteAsync(held.Id, ct);

        return new SignInResult.SignedIn(user, session, token);
    }
}
