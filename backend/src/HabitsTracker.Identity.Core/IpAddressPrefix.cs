using System.Net;
using System.Net.Sockets;

namespace HabitsTracker.Identity.Core;

// What code requests are counted by: an IPv4 address, or the /64 prefix of an IPv6 address.
public sealed record IpAddressPrefix
{
    private const int Ipv4PrefixLength = 32;
    private const int Ipv6PrefixLength = 64;

    private IpAddressPrefix(IPNetwork network)
    {
        Network = network;
    }

    public IPNetwork Network { get; }

    public static IpAddressPrefix From(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return new IpAddressPrefix(new IPNetwork(address, Ipv4PrefixLength));

        // IPNetwork refuses a base address with bits set after the prefix: clear the last 64 bits.
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, Ipv6PrefixLength / 8, bytes.Length - (Ipv6PrefixLength / 8));

        return new IpAddressPrefix(new IPNetwork(new IPAddress(bytes), Ipv6PrefixLength));
    }

    public static IpAddressPrefix FromStorage(IPNetwork network)
    {
        var expectedLength = network.BaseAddress.AddressFamily switch
        {
            AddressFamily.InterNetwork => Ipv4PrefixLength,
            AddressFamily.InterNetworkV6 => Ipv6PrefixLength,
            _ => -1,
        };

        if (network.PrefixLength != expectedLength)
            throw new DomainException(DomainError.CorruptRecord, "A stored IP address prefix breaks the prefix rules.");

        return new IpAddressPrefix(network);
    }

    public override string ToString() => Network.ToString();
}
