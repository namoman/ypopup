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

    public static IReadOnlyList<string> GetLocalSubnetBroadcastAddresses()
    {
        var broadcasts = new List<string>();

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
                    var ipBytes = address.Address.GetAddressBytes();
                    var maskBytes = address.IPv4Mask?.GetAddressBytes();
                    if (maskBytes == null || maskBytes.Length != 4)
                    {
                        continue;
                    }

                    var broadcastBytes = new byte[4];
                    for (int i = 0; i < 4; i++)
                    {
                        broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                    }

                    broadcasts.Add(new IPAddress(broadcastBytes).ToString());
                }
            }
        }

        return broadcasts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
