using System.Net;

namespace HabitsTracker.Identity.Core.Tests;

public class ValueObjectRulesTests
{
    [Fact]
    public void Secrets_do_not_print()
    {
        Assert.Equal("***", OneTimeCodeValue.Create("042917").ToString());
        Assert.Equal("***", CodeHash.FromStorage(new byte[32]).ToString());
        Assert.Equal("***", SessionToken.Generate().ToString());
    }

    [Fact]
    public void Code_hashes_are_equal_by_content()
    {
        var first = CodeHash.FromStorage(new byte[32]);
        var second = CodeHash.FromStorage(new byte[32]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void From_storage_rejects_corrupt_values()
    {
        AssertCorruptRecord(() => TimeZone.FromStorage("Mars/Olympus_Mons", true));
        AssertCorruptRecord(() => TimeZone.FromStorage("Europe/Warsaw", false));
        AssertCorruptRecord(() => CodeHash.FromStorage(new byte[31]));
    }

    [Fact]
    public void Configuration_values_are_checked()
    {
        var day = TimeSpan.FromHours(24);

        AssertInvalidConfiguration(() => CodeRequestLimits.Create(0, 10, day));
        AssertInvalidConfiguration(() => CodeRequestLimits.Create(5, 0, day));
        AssertInvalidConfiguration(() => CodeRequestLimits.Create(5, 10, TimeSpan.Zero));
        AssertInvalidConfiguration(() => CodeRequestLimits.Create(-1, 10, day));
        AssertInvalidConfiguration(() => CodeRequestLimits.Create(5, -1, day));
        AssertInvalidConfiguration(() => CodeRequestLimits.Create(5, 10, -day));
        AssertInvalidConfiguration(() => IdentityContextOptions.Create(new byte[31], CodeRequestLimits.Default));
    }

    [Fact]
    public void Options_keep_a_copy_of_the_code_hash_key()
    {
        var key = new byte[32];
        var options = IdentityContextOptions.Create(key, CodeRequestLimits.Default);

        key[0] = 1;

        Assert.Equal(new byte[32], options.CodeHashKey.ToArray());
        Assert.Same(CodeRequestLimits.Default, options.CodeRequestLimits);
    }

    [Fact]
    public void Instants_must_be_utc()
    {
        var email = Email.Create("ann@example.com");
        var zone = TimeZone.FromBrowser("Europe/Warsaw");
        var locale = Locale.FromStorage("pl");
        var warsawNoon = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => User.SignUp(email, zone, locale, warsawNoon));
        Assert.Throws<ArgumentException>(() => User.FromStorage(Guid.CreateVersion7(), email, zone, locale, warsawNoon));
        Assert.Throws<ArgumentException>(() => OneTimeCode.Issue(email, CodeHash.FromStorage(new byte[32]), warsawNoon));
        Assert.Throws<ArgumentException>(() => CodeRequest.Create(email, IpAddressPrefix.From(IPAddress.Loopback), warsawNoon));
        Assert.Throws<ArgumentException>(() => Session.Open(Guid.CreateVersion7(), remembered: false, warsawNoon));
    }

    [Fact]
    public void Unset_values_are_programming_errors()
    {
        var email = Email.Create("ann@example.com");
        var zone = TimeZone.FromBrowser("Europe/Warsaw");
        var locale = Locale.FromStorage("pl");
        var hash = CodeHash.FromStorage(new byte[32]);
        var prefix = IpAddressPrefix.From(IPAddress.Loopback);
        var code = OneTimeCodeValue.Create("042917");
        var now = DateTimeOffset.UnixEpoch;

        Assert.Throws<ArgumentException>(() => Session.Open(Guid.Empty, false, now));
        Assert.Throws<ArgumentException>(() => Session.FromStorage(new byte[32], Guid.Empty, false, now, now));

        Assert.Throws<ArgumentException>(() => User.SignUp(email, zone, locale, default));
        Assert.Throws<ArgumentException>(() => OneTimeCode.Issue(email, hash, default));
        Assert.Throws<ArgumentException>(() => CodeRequest.Create(email, prefix, default));
        Assert.Throws<ArgumentException>(() => Session.Open(Guid.CreateVersion7(), false, default));

        Assert.Throws<ArgumentException>(() => CodeHash.Compute(default, email, code));
        Assert.Throws<ArgumentException>(() => CodeHash.Compute(new byte[31], email, code));
    }

    [Fact]
    public void Null_arguments_are_programming_errors()
    {
        Assert.Throws<ArgumentNullException>(() => Email.FromStorage(null!));
        Assert.Throws<ArgumentNullException>(() => TimeZone.FromStorage(null!, true));
        Assert.Throws<ArgumentNullException>(() => Locale.FromStorage(null!));
        Assert.Throws<ArgumentNullException>(() => Locale.Create("pl", null!));
        Assert.Throws<ArgumentNullException>(() => IpAddressPrefix.From(null!));
        Assert.Throws<ArgumentNullException>(() => CodeHash.FromStorage(null!));
        Assert.Throws<ArgumentNullException>(() => CodeHash.Compute(new byte[32], null!, OneTimeCodeValue.Create("042917")));
        Assert.Throws<ArgumentNullException>(() => CodeHash.Compute(new byte[32], Email.Create("ann@example.com"), null!));
        Assert.Throws<ArgumentNullException>(() => IdentityContextOptions.Create(null!, CodeRequestLimits.Default));
        Assert.Throws<ArgumentNullException>(() => IdentityContextOptions.Create(new byte[32], null!));

        var email = Email.Create("ann@example.com");
        var zone = TimeZone.FromBrowser("Europe/Warsaw");
        var locale = Locale.FromStorage("pl");
        var now = DateTimeOffset.UnixEpoch;
        Assert.Throws<ArgumentNullException>(() => User.SignUp(null!, zone, locale, now));
        Assert.Throws<ArgumentNullException>(() => User.SignUp(email, null!, locale, now));
        Assert.Throws<ArgumentNullException>(() => User.SignUp(email, zone, null!, now));
        Assert.Throws<ArgumentNullException>(() => User.FromStorage(Guid.CreateVersion7(), null!, zone, locale, now));
        Assert.Throws<ArgumentNullException>(() => User.FromStorage(Guid.CreateVersion7(), email, null!, locale, now));
        Assert.Throws<ArgumentNullException>(() => User.FromStorage(Guid.CreateVersion7(), email, zone, null!, now));
        Assert.Throws<ArgumentNullException>(() => User.SignUp(email, zone, locale, now).ChangeLocale(null!));

        var hash = CodeHash.FromStorage(new byte[32]);
        Assert.Throws<ArgumentNullException>(() => OneTimeCode.Issue(null!, hash, now));
        Assert.Throws<ArgumentNullException>(() => OneTimeCode.Issue(email, null!, now));

        var prefix = IpAddressPrefix.From(IPAddress.Loopback);
        Assert.Throws<ArgumentNullException>(() => CodeRequest.Create(null!, prefix, now));
        Assert.Throws<ArgumentNullException>(() => CodeRequest.Create(email, null!, now));
        Assert.Throws<ArgumentNullException>(() => Session.FromStorage(null!, Guid.CreateVersion7(), false, now, now));
    }

    private static void AssertCorruptRecord(Action load)
    {
        var exception = Assert.Throws<DomainException>(load);

        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }

    private static void AssertInvalidConfiguration(Action create)
    {
        var exception = Assert.Throws<DomainException>(create);

        Assert.Equal(DomainError.InvalidConfiguration, exception.Error);
    }
}
