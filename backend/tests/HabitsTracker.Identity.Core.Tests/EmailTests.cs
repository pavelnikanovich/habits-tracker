namespace HabitsTracker.Identity.Core.Tests;

public class EmailTests
{
    public static TheoryData<string, string> Valid => new()
    {
        { "ann@example.com", "ann@example.com" },
        { "Ann@Example.COM", "ann@example.com" },
        { "a.b+tag@sub.example.com", "a.b+tag@sub.example.com" },
        // The WHATWG pattern needs no dot in the domain.
        { "a@b", "a@b" },
        // 254 characters, the maximum length.
        { new string('a', 242) + "@example.com", new string('a', 242) + "@example.com" },
    };

    public static TheoryData<string?> Invalid => new()
    {
        // 255 characters, one above the maximum length.
        new string('a', 243) + "@example.com",
        // A huge input gives a domain error; the length check runs before the pattern (research R19, D5),
        // held by the code, not observable here.
        new string('a', 100_000),
        (string?)null,
        "",
        "   ",
        // No trimming, no whitespace anywhere.
        "  ann@example.com ",
        "ann@example.com\t",
        "ann\n@example.com",
        "ann@example.com\n",
        "ann @example.com",
        "ann@exa mple.com",
        "ann",
        "@example.com",
        "ann@",
        "ann@b@example.com",
        "ann@-example.com",
        "ann@example-.com",
        // Cyrillic "а": ASCII only.
        "аnn@example.com",
    };

    [Theory]
    [MemberData(nameof(Valid))]
    public void Creates(string input, string expectedValue)
    {
        var email = Email.Create(input);

        Assert.Equal(expectedValue, email.Value);
    }

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Rejects(string? input)
    {
        var exception = Assert.Throws<DomainException>(() => Email.Create(input));

        Assert.Equal(DomainError.InvalidEmail, exception.Error);
    }

    [Fact]
    public void Emails_differing_in_case_are_equal()
    {
        Assert.Equal(Email.Create("Ann@Example.com"), Email.Create("ann@example.COM"));
    }

    [Fact]
    public void From_storage_keeps_a_lower_cased_value()
    {
        Assert.Equal("ann@example.com", Email.FromStorage("ann@example.com").Value);
    }

    [Fact]
    public void From_storage_rejects_a_value_that_is_not_lower_cased()
    {
        var exception = Assert.Throws<DomainException>(() => Email.FromStorage("Ann@example.com"));

        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }
}
