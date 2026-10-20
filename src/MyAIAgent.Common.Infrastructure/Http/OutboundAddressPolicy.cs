/**
 * MyAIAgent | Common · Infrastructure
 *
 * Classifies outbound addresses conservatively using IANA special-purpose ranges and Azure platform exclusions.
 */

using System.Net;
using System.Net.Sockets;

namespace MyAIAgent.Common.Infrastructure.Http;

public static class OutboundAddressPolicy
{
    // IANA IPv4/IPv6 Special-Purpose Address Registries, reviewed 2026-10-20.
    // Special-purpose blocks are excluded even when a more specific assignment is globally reachable.
    // https://www.iana.org/assignments/iana-ipv4-special-registry/
    // https://www.iana.org/assignments/iana-ipv6-special-registry/
    private static readonly IPNetwork[] Excluded =
    [
        IPNetwork.Parse("0.0.0.0/8"), IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"), IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"), IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.31.196.0/24"), IPNetwork.Parse("192.52.193.0/24"),
        IPNetwork.Parse("192.88.99.0/24"), IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("192.175.48.0/24"), IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"), IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/3"),
        IPNetwork.Parse("2001::/23"), IPNetwork.Parse("2001:db8::/32"),
        IPNetwork.Parse("2002::/16"), IPNetwork.Parse("2620:4f:8000::/48"),
        IPNetwork.Parse("3fff::/20")
    ];

    public static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
            return false;
        address = Normalize(address);
        if (address.AddressFamily == AddressFamily.InterNetworkV6
            && (address.ScopeId != 0 || !IPNetwork.Parse("2000::/3").Contains(address)))
            return false;
        if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            return false;
        // Azure WireServer uses an otherwise public-looking address.
        // https://learn.microsoft.com/en-us/azure/virtual-network/what-is-ip-address-168-63-129-16
        return !address.Equals(IPAddress.Parse("168.63.129.16"))
            && !Excluded.Any(network => network.Contains(address));
    }

    public static bool IsPrivateAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
            return false;
        address = Normalize(address);
        // Cloud metadata addresses must not inherit the first-party unique-local exception.
        // https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/configuring-instance-metadata-service.html
        // https://cloud.google.com/compute/docs/metadata/querying-metadata
        if (address.Equals(IPAddress.Parse("fd00:ec2::254")) || address.Equals(IPAddress.Parse("fd20:ce::254")))
            return false;
        return (address.AddressFamily != AddressFamily.InterNetworkV6 || address.ScopeId == 0)
            && (IPNetwork.Parse("10.0.0.0/8").Contains(address)
                || IPNetwork.Parse("172.16.0.0/12").Contains(address)
                || IPNetwork.Parse("192.168.0.0/16").Contains(address)
                || IPNetwork.Parse("fc00::/7").Contains(address));
    }

    public static bool HasSafeAuthority(Uri uri) => uri.IsAbsoluteUri
        && uri.Scheme is "http" or "https"
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0
        && !uri.IdnHost.Contains('%');

    public static bool IsPublicUri(Uri uri) => HasSafeAuthority(uri)
        && !uri.IsLoopback
        && !uri.IdnHost.TrimEnd('.').EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        && !uri.IdnHost.TrimEnd('.').Equals("localhost", StringComparison.OrdinalIgnoreCase)
        && (!IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out var address) || IsPublicAddress(address));
}
