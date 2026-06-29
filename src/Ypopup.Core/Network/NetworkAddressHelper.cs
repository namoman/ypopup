using System.Net;
using System.Net.Sockets;

namespace Ypopup.Core.Network;

public static class NetworkAddressHelper
{
    public static string NormalizeToConnectableAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return address.ToString();
        }

        return address.ToString();
    }

    public static IPAddress ParseConnectableAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var parsed))
        {
            throw new FormatException($"유효하지 않은 IP 주소입니다: {ipAddress}");
        }

        if (parsed.IsIPv4MappedToIPv6)
        {
            parsed = parsed.MapToIPv4();
        }

        return parsed;
    }
}
