using HabitsTracker.Identity.Core.Tests.Fakes;
using HabitsTracker.Identity.Core.UseCases;
using Microsoft.Extensions.Time.Testing;

namespace HabitsTracker.Identity.Core.Tests;

public class SignInTests
{
    private static readonly DateTimeOffset Issued = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Issued.AddMinutes(1);
    private static readonly Email Ann = Email.Create("ann@example.com");
    private static readonly Email Bob = Email.Create("bob@example.com");
    private static readonly Locale Polish = Locale.FromStorage("pl");
    private static readonly Locale Russian = Locale.FromStorage("ru");
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private const string AnnsCode = "111111";
    private const string BobsCode = "222222";

    private readonly CallLog calls = new();
    private readonly FakeTimeProvider time = new(Now);
    private readonly InMemoryTransaction transaction;
    private readonly InMemoryOneTimeCodes oneTimeCodes;
    private readonly InMemoryUsers users;
    private readonly InMemorySessions sessions;

    public SignInTests()
    {
        transaction = new InMemoryTransaction(calls);
        oneTimeCodes = new InMemoryOneTimeCodes(transaction, calls);
        users = new InMemoryUsers(transaction, calls);
        sessions = new InMemorySessions(transaction, calls);
    }

    [Fact]
    public async Task First_sign_in_creates_the_user_with_the_browser_time_zone_and_the_locale()
    {
        await IssueAsync(Ann, AnnsCode);

        var result = await SignInAsync(Ann, AnnsCode, browserTimeZone: "Europe/Warsaw", localeCode: "pl");

        var signedIn = Assert.IsType<SignInResult.SignedIn>(result);
        Assert.Equal(Ann, signedIn.User.Email);
        Assert.Equal("Europe/Warsaw", signedIn.User.TimeZone.Id);
        Assert.True(signedIn.User.TimeZone.Recognized);
        Assert.Equal(Polish, signedIn.User.Locale);
        Assert.Equal(Now, signedIn.User.CreatedAt);
        Assert.Equal(signedIn.User.Id, (await users.FindByEmailAsync(Ann, Ct))?.Id);
        Assert.Equal(signedIn.User.Id, signedIn.Session.UserId);
        Assert.False(signedIn.Session.Remembered);
        Assert.Equal(Now, signedIn.Session.LastActivityAt);
        Assert.Equal(signedIn.Token.ComputeId(), signedIn.Session.Id);
        Assert.NotNull(await sessions.FindAsync(signedIn.Session.Id, Ct));
        Assert.Null(await oneTimeCodes.FindExclusiveAsync(Ann, Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Mars/Olympus_Mons")]
    public async Task An_unknown_browser_time_zone_gives_UTC_not_recognized(string? browserTimeZone)
    {
        await IssueAsync(Ann, AnnsCode);

        var signedIn = Assert.IsType<SignInResult.SignedIn>(
            await SignInAsync(Ann, AnnsCode, browserTimeZone: browserTimeZone));

        Assert.Equal("UTC", signedIn.User.TimeZone.Id);
        Assert.False(signedIn.User.TimeZone.Recognized);
    }

    [Fact]
    public async Task A_later_sign_in_keeps_the_time_zone_and_the_locale()
    {
        var ann = await SignUpAsync(Ann, "Europe/Warsaw", Russian);
        await IssueAsync(Ann, AnnsCode);

        var signedIn = Assert.IsType<SignInResult.SignedIn>(
            await SignInAsync(Ann, AnnsCode, browserTimeZone: "America/New_York", localeCode: "en"));

        Assert.Equal(ann.Id, signedIn.User.Id);
        Assert.Equal("Europe/Warsaw", signedIn.User.TimeZone.Id);
        Assert.Equal(Russian, signedIn.User.Locale);
        var stored = await users.FindByEmailAsync(Ann, Ct);
        Assert.Equal("Europe/Warsaw", stored?.TimeZone.Id);
        Assert.Equal(Russian, stored?.Locale);
    }

    [Fact]
    public async Task Email_case_does_not_matter()
    {
        var ann = await SignUpAsync(Email.Create("Ann@example.com"), "Europe/Warsaw", Polish);
        await IssueAsync(Email.Create("ann@example.com"), AnnsCode);

        var signedIn = Assert.IsType<SignInResult.SignedIn>(
            await SignInAsync(Email.Create("ANN@Example.com"), AnnsCode));

        Assert.Equal(ann.Id, signedIn.User.Id);
    }

    [Fact]
    public async Task Remember_me_opens_a_remembered_session()
    {
        await IssueAsync(Ann, AnnsCode);

        var signedIn = Assert.IsType<SignInResult.SignedIn>(await SignInAsync(Ann, AnnsCode, rememberMe: true));

        Assert.True(signedIn.Session.Remembered);
        Assert.True((await sessions.FindAsync(signedIn.Session.Id, Ct))?.Remembered);
    }

    // Review Focus 1: a code entered a second time after a successful sign-in.
    [Fact]
    public async Task A_used_code_requires_a_new_code()
    {
        await IssueAsync(Ann, AnnsCode);
        Assert.IsType<SignInResult.SignedIn>(await SignInAsync(Ann, AnnsCode));

        Assert.IsType<SignInResult.NewCodeRequired>(await SignInAsync(Ann, AnnsCode));
    }

    // Review Focus 1: a code entered at exactly 10 minutes after issue.
    [Fact]
    public async Task A_code_at_ten_minutes_requires_a_new_code()
    {
        await IssueAsync(Ann, AnnsCode);
        time.SetUtcNow(Issued + OneTimeCode.Lifetime);

        Assert.IsType<SignInResult.NewCodeRequired>(await SignInAsync(Ann, AnnsCode));

        Assert.Null(await users.FindByEmailAsync(Ann, Ct));
    }

    [Fact]
    public async Task No_code_requires_a_new_code()
    {
        Assert.IsType<SignInResult.NewCodeRequired>(await SignInAsync(Ann, AnnsCode));

        Assert.Null(await users.FindByEmailAsync(Ann, Ct));
    }

    [Fact]
    public async Task A_wrong_code_is_counted()
    {
        await IssueAsync(Ann, AnnsCode);

        Assert.IsType<SignInResult.CodeIncorrect>(await SignInAsync(Ann, BobsCode));

        Assert.Equal("InMemoryTransaction.Commit", calls.Entries[^1]);
        Assert.Equal(1, await AttemptsAsync(Ann));
        Assert.Null(await users.FindByEmailAsync(Ann, Ct));
    }

    // Review Focus 5: a code sent to A, entered with email B.
    [Fact]
    public async Task Code_of_another_email_does_not_sign_in()
    {
        await IssueAsync(Ann, AnnsCode);
        await IssueAsync(Bob, BobsCode);

        Assert.IsType<SignInResult.CodeIncorrect>(await SignInAsync(Bob, AnnsCode));

        Assert.Equal(1, await AttemptsAsync(Bob));
        Assert.Equal(0, await AttemptsAsync(Ann));
        Assert.Null(await users.FindByEmailAsync(Bob, Ct));
        Assert.IsType<SignInResult.SignedIn>(await SignInAsync(Ann, AnnsCode));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_held_session_is_revoked_by_the_sign_in(bool asItsUser)
    {
        var ann = await SignUpAsync(Ann, "Europe/Warsaw", Polish);
        var (held, heldToken) = Session.Open(ann.Id, false, Issued);
        await sessions.AddAsync(held, Ct);
        var email = asItsUser ? Ann : Bob;
        await IssueAsync(email, AnnsCode);

        var signedIn = Assert.IsType<SignInResult.SignedIn>(
            await SignInAsync(email, AnnsCode, heldSession: heldToken));

        Assert.Null(await sessions.FindAsync(held.Id, Ct));
        Assert.NotNull(await sessions.FindAsync(signedIn.Session.Id, Ct));
    }

    [Fact]
    public async Task An_unknown_held_session_deletes_nothing()
    {
        var ann = await SignUpAsync(Ann, "Europe/Warsaw", Polish);
        var (other, _) = Session.Open(ann.Id, false, Issued);
        await sessions.AddAsync(other, Ct);
        await IssueAsync(Ann, AnnsCode);

        var signedIn = Assert.IsType<SignInResult.SignedIn>(
            await SignInAsync(Ann, AnnsCode, heldSession: SessionToken.Generate()));

        Assert.NotNull(await sessions.FindAsync(other.Id, Ct));
        Assert.NotNull(await sessions.FindAsync(signedIn.Session.Id, Ct));
    }

    [Fact]
    public async Task Uses_the_code_and_opens_the_session_in_one_transaction()
    {
        await IssueAsync(Ann, AnnsCode);

        await SignInAsync(Ann, AnnsCode);

        Assert.Equal(
            [
                "InMemoryOneTimeCodes.ReplaceAsync",
                "InMemoryTransaction.Begin",
                "InMemoryOneTimeCodes.FindExclusiveAsync",
                "InMemoryOneTimeCodes.SaveAsync",
                "InMemoryUsers.FindByEmailAsync",
                "InMemoryUsers.AddOrGetAsync",
                "InMemorySessions.AddAsync",
                "InMemoryTransaction.Commit",
            ],
            calls.Entries);
    }

    [Fact]
    public async Task Unset_arguments_are_programming_errors()
    {
        var useCase = NewSignIn();
        var code = OneTimeCodeValue.Create(AnnsCode);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(null!, code, false, null, "pl", null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(Ann, null!, false, null, "pl", null, Ct));
    }

    // M4: the locale is built before the transaction, whether the email has a user or not, so the answer to an
    // unavailable locale never tells which (Review Focus 3).
    [Fact]
    public async Task An_unavailable_locale_is_refused_before_the_transaction()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => SignInAsync(Ann, AnnsCode, localeCode: "de"));

        Assert.Equal(DomainError.InvalidLocale, exception.Error);
        Assert.Empty(calls.Entries);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SignIn NewSignIn() =>
        new(transaction, oneTimeCodes, users, sessions, new FixedLocales(), time,
            IdentityContextOptions.Create(Key, CodeRequestLimits.Default));

    private Task<SignInResult> SignInAsync(
        Email email,
        string digits,
        bool rememberMe = false,
        string? browserTimeZone = "Europe/Warsaw",
        string? localeCode = "pl",
        SessionToken? heldSession = null) =>
        NewSignIn().ExecuteAsync(
            email, OneTimeCodeValue.Create(digits), rememberMe, browserTimeZone, localeCode, heldSession, Ct);

    private Task IssueAsync(Email email, string digits) =>
        oneTimeCodes.ReplaceAsync(
            OneTimeCode.Issue(email, CodeHash.Compute(Key, email, OneTimeCodeValue.Create(digits)), Issued), Ct);

    private Task<User> SignUpAsync(Email email, string timeZone, Locale locale) =>
        users.AddOrGetAsync(User.SignUp(email, TimeZone.FromBrowser(timeZone), locale, Issued.AddDays(-10)), Ct);

    private async Task<int> AttemptsAsync(Email email) =>
        (await oneTimeCodes.FindExclusiveAsync(email, Ct))?.EntryAttempts
        ?? throw new Xunit.Sdk.XunitException($"No code is stored for {email.Value}.");
}
