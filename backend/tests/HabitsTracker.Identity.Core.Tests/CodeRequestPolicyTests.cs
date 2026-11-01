using System.Diagnostics;

namespace HabitsTracker.Identity.Core.Tests;

public class CodeRequestPolicyTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    // Hours are relative to T; the expected value is the remaining count for Allowed, else the hours to retryAt.
    [Theory]
    [InlineData(0, null, 0, null, "Allowed", 4)]
    [InlineData(3, -5, 3, -5, "Allowed", 1)]
    [InlineData(4, -5, 9, -21, "Allowed", 0)]
    [InlineData(5, -23, 5, -24, "RefusedByEmailLimit", 1)]
    [InlineData(4, -2, 10, -21, "RefusedByIpLimit", 3)]
    [InlineData(5, -23, 10, -21, "RefusedByEmailLimit", 1)]
    public void Decides(
        int forEmail, int? oldestForEmail, int forIp, int? oldestForIp, string decision, int expectedValue)
    {
        var counts = new CodeRequestCounts(forEmail, At(oldestForEmail), forIp, At(oldestForIp));
        CodeRequestDecision expected = decision switch
        {
            "Allowed" => new CodeRequestDecision.Allowed(expectedValue),
            "RefusedByEmailLimit" => new CodeRequestDecision.RefusedByEmailLimit(T.AddHours(expectedValue)),
            "RefusedByIpLimit" => new CodeRequestDecision.RefusedByIpLimit(T.AddHours(expectedValue)),
            _ => throw new UnreachableException(),
        };

        Assert.Equal(expected, CodeRequestPolicy.Decide(counts, CodeRequestLimits.Default));
    }

    [Fact]
    public void A_count_at_the_limit_without_its_oldest_moment_is_corrupt()
    {
        var counts = new CodeRequestCounts(5, null, 0, null);

        var exception = Assert.Throws<DomainException>(() => CodeRequestPolicy.Decide(counts, CodeRequestLimits.Default));

        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }

    [Fact]
    public void An_ip_address_prefix_count_at_the_limit_without_its_oldest_moment_is_corrupt()
    {
        var counts = new CodeRequestCounts(0, null, 10, null);

        var exception = Assert.Throws<DomainException>(() => CodeRequestPolicy.Decide(counts, CodeRequestLimits.Default));

        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }

    [Fact]
    public void A_negative_count_is_corrupt()
    {
        AssertCorrupt(new CodeRequestCounts(-1, null, 0, null));
        AssertCorrupt(new CodeRequestCounts(0, null, -1, null));
    }

    [Fact]
    public void A_count_below_the_limit_without_its_oldest_moment_is_corrupt()
    {
        AssertCorrupt(new CodeRequestCounts(1, null, 0, null));
        AssertCorrupt(new CodeRequestCounts(0, null, 1, null));
    }

    [Fact]
    public void An_oldest_moment_without_a_count_is_corrupt()
    {
        AssertCorrupt(new CodeRequestCounts(0, T.AddHours(-1), 0, null));
        AssertCorrupt(new CodeRequestCounts(0, null, 0, T.AddHours(-1)));
    }

    [Fact]
    public void An_oldest_moment_that_is_unset_or_not_utc_is_corrupt()
    {
        var notUtc = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(2));

        AssertCorrupt(new CodeRequestCounts(1, default(DateTimeOffset), 0, null));
        AssertCorrupt(new CodeRequestCounts(1, notUtc, 0, null));
        AssertCorrupt(new CodeRequestCounts(0, null, 1, default(DateTimeOffset)));
        AssertCorrupt(new CodeRequestCounts(0, null, 1, notUtc));
    }

    private static void AssertCorrupt(CodeRequestCounts counts)
    {
        var exception = Assert.Throws<DomainException>(() => CodeRequestPolicy.Decide(counts, CodeRequestLimits.Default));

        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }

    private static DateTimeOffset? At(int? hours) => hours is { } h ? T.AddHours(h) : null;
}
