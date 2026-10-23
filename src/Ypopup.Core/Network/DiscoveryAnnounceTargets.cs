using System.Net;
using Ypopup.Core.Models;

namespace Ypopup.Core.Network;

public static class DiscoveryAnnounceTargets
{
    public static readonly IPAddress MulticastGroup = IPAddress.Parse(AppConstants.DiscoveryMulticastAddress);

    public static bool IsIpv4LocalScope(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 239 && bytes[1] == 255;
    }

    public static IReadOnlyList<IPEndPoint> Build(IReadOnlyList<string> broadcastAddresses, int port)
    {
        var endpoints = new List<IPEndPoint>();

        if (broadcastAddresses.Count == 0)
        {
            endpoints.Add(new IPEndPoint(IPAddress.Broadcast, port));
        }
        else
        {
            foreach (var addressText in broadcastAddresses)
            {
                if (IPAddress.TryParse(addressText, out var address)
                    && !endpoints.Exists(endpoint => endpoint.Address.Equals(address)))
                {
                    endpoints.Add(new IPEndPoint(address, port));
                }
            }
        }

        if (!endpoints.Exists(endpoint => endpoint.Address.Equals(MulticastGroup)))
        {
            endpoints.Add(new IPEndPoint(MulticastGroup, port));
        }

        return endpoints;
    }
}
