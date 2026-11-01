using System.Net;

namespace HabitsTracker.Identity.Core.Tests;

public class IpAddressPrefixTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7/32")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:ffff:ffff:ffff:ffff", "2001:db8:1:2::/64")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7/32")]
    public void From_address(string address, string expectedPrefix)
    {
        var prefix = IpAddressPrefix.From(IPAddress.Parse(address));

        Assert.Equal(expectedPrefix, prefix.ToString());
    }

    [Fact]
    public void Addresses_in_one_64_prefix_are_equal()
    {
        Assert.Equal(
            IpAddressPrefix.From(IPAddress.Parse("2001:db8:1:2::1")),
            IpAddressPrefix.From(IPAddress.Parse("2001:db8:1:2:ffff::")));
    }

    [Theory]
    [InlineData("203.0.113.7/32")]
    [InlineData("2001:db8:1:2::/64")]
    public void From_storage_keeps_a_valid_prefix(string network)
    {
        var prefix = IpAddressPrefix.FromStorage(IPNetwork.Parse(network));

        Assert.Equal(IPNetwork.Parse(network), prefix.Network);
    }

    [Theory]
    [InlineData("203.0.113.0/24")]
    [InlineData("2001:db8::/48")]
    public void From_storage_rejects_other_prefix_lengths(string network)
    {
        var exception = Assert.Throws<DomainException>(() => IpAddressPrefix.FromStorage(IPNetwork.Parse(network)));

        Assert.Equal(DomainError.CorruptRecord, exception.Error);
    }
}
