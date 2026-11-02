using System.Net;
using HabitsTracker.Identity.Core;

namespace HabitsTracker.Tests.PortContracts;

// The contract of ICodeRequests (data-model.md, "Driven ports"): the PostgreSQL store and the in-memory fake pass it.
// LockAsync is a PostgreSQL lock, tested in the storage adapter's concurrency tests only.
public abstract class CodeRequestsContract
{
    private static readonly DateTimeOffset T = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email Ada = Email.Create("ada@example.com");
    private static readonly IpAddressPrefix Address = IpAddressPrefix.From(IPAddress.Parse("192.0.2.1"));

    protected abstract ICodeRequests CodeRequests { get; }

    [Fact]
    public async Task Count_uses_a_sliding_window()
    {
        var ct = TestContext.Current.CancellationToken;
        var windowStart = T - TimeSpan.FromHours(24);
        var oldestCounted = windowStart + TimeSpan.FromSeconds(1);
        await CodeRequests.AddAsync(CodeRequest.Create(Ada, Address, windowStart), ct);
        await CodeRequests.AddAsync(CodeRequest.Create(Ada, Address, oldestCounted), ct);
        await CodeRequests.AddAsync(CodeRequest.Create(Ada, Address, T - TimeSpan.FromHours(1)), ct);
        var otherAddress = IpAddressPrefix.From(IPAddress.Parse("198.51.100.1"));
        await CodeRequests.AddAsync(CodeRequest.Create(Email.Create("bob@example.com"), otherAddress, T - TimeSpan.FromHours(1)), ct);

        var counts = await CodeRequests.CountAsync(Ada, Address, windowStart, ct);

        Assert.Equal(new CodeRequestCounts(2, oldestCounted, 2, oldestCounted), counts);
    }

    [Fact]
    public async Task Requests_of_one_64_prefix_count_together()
    {
        var ct = TestContext.Current.CancellationToken;
        var prefix = IpAddressPrefix.From(IPAddress.Parse("2001:db8:1:2::1"));
        var samePrefix = IpAddressPrefix.From(IPAddress.Parse("2001:db8:1:2:ffff::9"));
        var otherPrefix = IpAddressPrefix.From(IPAddress.Parse("2001:db8:1:3::1"));
        await CodeRequests.AddAsync(CodeRequest.Create(Ada, prefix, T - TimeSpan.FromHours(2)), ct);
        await CodeRequests.AddAsync(CodeRequest.Create(Email.Create("bob@example.com"), samePrefix, T - TimeSpan.FromHours(1)), ct);
        await CodeRequests.AddAsync(CodeRequest.Create(Email.Create("cy@example.com"), otherPrefix, T - TimeSpan.FromMinutes(30)), ct);

        var counts = await CodeRequests.CountAsync(Ada, prefix, T - TimeSpan.FromHours(24), ct);

        Assert.Equal(new CodeRequestCounts(1, T - TimeSpan.FromHours(2), 2, T - TimeSpan.FromHours(2)), counts);
    }

    [Fact]
    public async Task Delete_requested_at_or_before()
    {
        var ct = TestContext.Current.CancellationToken;
        var cutoff = T - TimeSpan.FromHours(24);
        var kept = cutoff + TimeSpan.FromSeconds(1);
        await CodeRequests.AddAsync(CodeRequest.Create(Ada, Address, cutoff), ct);
        await CodeRequests.AddAsync(CodeRequest.Create(Ada, Address, kept), ct);

        Assert.Equal(1, await CodeRequests.DeleteRequestedAtOrBeforeAsync(cutoff, ct));

        var counts = await CodeRequests.CountAsync(Ada, Address, cutoff - TimeSpan.FromDays(1), ct);
        Assert.Equal(new CodeRequestCounts(1, kept, 1, kept), counts);
    }
}
