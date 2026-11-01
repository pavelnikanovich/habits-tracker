using System.Net;

namespace HabitsTracker.Identity.Core.Tests;

public class AggregateTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 1, 9, 30, 0, TimeSpan.Zero);
    private static readonly Email Ann = Email.Create("ann@example.com");
    private static readonly TimeZone Warsaw = TimeZone.FromBrowser("Europe/Warsaw");
    private static readonly Locale Polish = Locale.FromStorage("pl");
    private static readonly Locale Russian = Locale.FromStorage("ru");
    private static readonly CodeHash Hash = CodeHash.FromStorage(new byte[32]);
    private static readonly IpAddressPrefix Prefix = IpAddressPrefix.From(IPAddress.Parse("203.0.113.7"));

    [Fact]
    public void Default_code_request_limits_are_five_per_email_and_ten_per_ip_address_in_24_hours()
    {
        Assert.Equal(5, CodeRequestLimits.Default.PerEmail);
        Assert.Equal(10, CodeRequestLimits.Default.PerIpAddress);
        Assert.Equal(TimeSpan.FromHours(24), CodeRequestLimits.Default.Window);
    }

    [Fact]
    public void Sign_up_sets_every_field_and_a_version_7_id()
    {
        var user = User.SignUp(Ann, Warsaw, Polish, T);

        Assert.Equal(7, user.Id.Version);
        Assert.Equal(Ann, user.Email);
        Assert.Equal(Warsaw, user.TimeZone);
        Assert.Equal(Polish, user.Locale);
        Assert.Equal(T, user.CreatedAt);
    }

    [Fact]
    public void Changing_the_locale_changes_only_the_locale()
    {
        var user = User.SignUp(Ann, Warsaw, Polish, T);
        var id = user.Id;

        user.ChangeLocale(Russian);

        Assert.Equal(Russian, user.Locale);
        Assert.Equal(id, user.Id);
        Assert.Equal(Ann, user.Email);
        Assert.Equal(Warsaw, user.TimeZone);
        Assert.Equal(T, user.CreatedAt);
    }

    [Fact]
    public void User_loaded_from_storage_keeps_every_field()
    {
        var id = Guid.CreateVersion7(T);

        var user = User.FromStorage(id, Ann, Warsaw, Polish, T);

        Assert.Equal(id, user.Id);
        Assert.Equal(Ann, user.Email);
        Assert.Equal(Warsaw, user.TimeZone);
        Assert.Equal(Polish, user.Locale);
        Assert.Equal(T, user.CreatedAt);
    }

    [Fact]
    public void Issued_code_expires_in_ten_minutes_with_no_entry_attempts()
    {
        var code = OneTimeCode.Issue(Ann, Hash, T);

        Assert.Equal(Ann, code.Email);
        Assert.Equal(Hash, code.CodeHash);
        Assert.Equal(T, code.IssuedAt);
        Assert.Equal(T + TimeSpan.FromMinutes(10), code.ExpiresAt);
        Assert.Equal(0, code.EntryAttempts);
        Assert.False(code.IsUsed);
        Assert.Equal(TimeSpan.FromMinutes(10), OneTimeCode.Lifetime);
        Assert.Equal(5, OneTimeCode.MaxEntryAttempts);
    }

    [Fact]
    public void Code_loaded_from_storage_keeps_every_field()
    {
        var code = OneTimeCode.FromStorage(Ann, Hash, T, T + TimeSpan.FromMinutes(10), 5);

        Assert.Equal(Ann, code.Email);
        Assert.Equal(Hash, code.CodeHash);
        Assert.Equal(T, code.IssuedAt);
        Assert.Equal(T + TimeSpan.FromMinutes(10), code.ExpiresAt);
        Assert.Equal(5, code.EntryAttempts);
        Assert.False(code.IsUsed);
    }

    [Fact]
    public void New_code_request_has_no_id_until_stored()
    {
        var request = CodeRequest.Create(Ann, Prefix, T);

        Assert.Equal(0, request.Id);
        Assert.Equal(Ann, request.Email);
        Assert.Equal(Prefix, request.IpAddressPrefix);
        Assert.Equal(T, request.RequestedAt);
    }

    [Fact]
    public void Code_request_loaded_from_storage_keeps_every_field()
    {
        var request = CodeRequest.FromStorage(42, Ann, Prefix, T);

        Assert.Equal(42, request.Id);
        Assert.Equal(Ann, request.Email);
        Assert.Equal(Prefix, request.IpAddressPrefix);
        Assert.Equal(T, request.RequestedAt);
    }

    [Fact]
    public void Opened_session_is_identified_by_its_token()
    {
        var userId = Guid.CreateVersion7(T);

        var (session, token) = Session.Open(userId, remembered: true, T);

        Assert.Equal(token.ComputeId(), session.Id);
        Assert.Equal(userId, session.UserId);
        Assert.True(session.Remembered);
        Assert.Equal(T, session.CreatedAt);
        Assert.Equal(T, session.LastActivityAt);
        Assert.Equal(TimeSpan.FromDays(30), Session.IdleTimeout);
    }

    [Fact]
    public void Session_loaded_from_storage_keeps_every_field()
    {
        var id = new byte[32];
        var userId = Guid.CreateVersion7(T);

        var session = Session.FromStorage(id, userId, remembered: false, T, T + TimeSpan.FromHours(1));

        Assert.Equal(id, session.Id);
        Assert.Equal(userId, session.UserId);
        Assert.False(session.Remembered);
        Assert.Equal(T, session.CreatedAt);
        Assert.Equal(T + TimeSpan.FromHours(1), session.LastActivityAt);
    }

    [Fact]
    public void Aggregates_loaded_from_storage_check_their_invariants()
    {
        AssertCorruptRecord(() => User.FromStorage(Guid.Empty, Ann, Warsaw, Polish, T));
        AssertCorruptRecord(() => OneTimeCode.FromStorage(Ann, Hash, T, T + TimeSpan.FromMinutes(11), 0));
        AssertCorruptRecord(() => OneTimeCode.FromStorage(Ann, Hash, T, T + TimeSpan.FromMinutes(10), 6));
        AssertCorruptRecord(() => OneTimeCode.FromStorage(Ann, Hash, T, T + TimeSpan.FromMinutes(10), -1));
        AssertCorruptRecord(() => CodeRequest.FromStorage(0, Ann, Prefix, T));
        AssertCorruptRecord(() => CodeRequest.FromStorage(-1, Ann, Prefix, T));
        AssertCorruptRecord(() => Session.FromStorage(new byte[31], Guid.CreateVersion7(T), false, T, T));
        AssertCorruptRecord(() => Session.FromStorage(new byte[32], Guid.Empty, false, T, T));
        AssertCorruptRecord(() => Session.FromStorage(new byte[32], Guid.CreateVersion7(T), false, T, T - TimeSpan.FromTicks(1)));
    }

    private static void AssertCorruptRecord(Action load)
    {
        var exception = Assert.Throws<DomainException>(load);

        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }
}
