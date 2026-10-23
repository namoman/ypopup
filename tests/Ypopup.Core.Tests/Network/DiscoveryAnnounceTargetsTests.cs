using System.Net;
using Xunit;
using Ypopup.Core.Models;
using Ypopup.Core.Network;

namespace Ypopup.Core.Tests.Network;

public class DiscoveryAnnounceTargetsTests
{
    [Fact]
    public void MulticastGroup_IsIpv4LocalScope()
    {
        Assert.Equal(AppConstants.DiscoveryMulticastAddress, DiscoveryAnnounceTargets.MulticastGroup.ToString());
        Assert.True(DiscoveryAnnounceTargets.IsIpv4LocalScope(DiscoveryAnnounceTargets.MulticastGroup));
        Assert.False(DiscoveryAnnounceTargets.IsIpv4LocalScope(IPAddress.Parse("239.192.0.1")));
        Assert.False(DiscoveryAnnounceTargets.IsIpv4LocalScope(IPAddress.Parse("224.0.0.1")));
    }

    [Fact]
    public void Build_WithBroadcastAddresses_AppendsMulticastOnce()
    {
        var endpoints = DiscoveryAnnounceTargets.Build(["192.168.0.255", "10.0.0.255"], port: 50505);
        var addresses = endpoints.Select(endpoint => endpoint.Address.ToString()).ToArray();

        Assert.Equal(new[] { "192.168.0.255", "10.0.0.255", AppConstants.DiscoveryMulticastAddress }, addresses);
        Assert.All(endpoints, endpoint => Assert.Equal(50505, endpoint.Port));
    }

    [Fact]
    public void Build_WithoutBroadcastAddresses_UsesLimitedBroadcastThenMulticast()
    {
        var endpoints = DiscoveryAnnounceTargets.Build([], port: 50506);
        var addresses = endpoints.Select(endpoint => endpoint.Address.ToString()).ToArray();

        Assert.Equal(new[] { IPAddress.Broadcast.ToString(), AppConstants.DiscoveryMulticastAddress }, addresses);
        Assert.All(endpoints, endpoint => Assert.Equal(50506, endpoint.Port));
    }

    [Fact]
    public void Build_WhenMulticastIsAlreadyListed_KeepsItOnce()
    {
        var endpoints = DiscoveryAnnounceTargets.Build(
            ["192.168.0.255", AppConstants.DiscoveryMulticastAddress],
            port: 50505);

        Assert.Equal(1, endpoints.Count(endpoint => endpoint.Address.Equals(DiscoveryAnnounceTargets.MulticastGroup)));
        Assert.Equal("192.168.0.255", endpoints[0].Address.ToString());
    }
}
