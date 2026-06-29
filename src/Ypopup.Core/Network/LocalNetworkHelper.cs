using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ypopup.Core.Network;

public static class LocalNetworkHelper
{
    public static IReadOnlyList<string> GetLocalIPv4Addresses()
    {
        var addresses = new List<string>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address.Address))
                {
                    addresses.Add(address.Address.ToString());
                }
            }
        }

        return addresses.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static ip => ip).ToList();
    }

    public static string ResolvePreferredIp(string preferredLocalIp)
    {
        var addresses = GetLocalIPv4Addresses();
        if (addresses.Count == 0)
        {
            return "127.0.0.1";
        }

        if (!string.IsNullOrWhiteSpace(preferredLocalIp)
            && addresses.Contains(preferredLocalIp, StringComparer.OrdinalIgnoreCase))
        {
            return preferredLocalIp;
        }

        return addresses[0];
    }
}
