using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Ypopup.Core.Models;
using Ypopup.Core.Network;

namespace Ypopup.Core.Diagnostics;

public static class DiagnosticExporter
{
    public static string Generate(AppSettings settings, IReadOnlyList<PeerInfo> peers, string? logDirectory = null)
    {
        var sb = new StringBuilder();
        var now = DateTime.Now;

        sb.AppendLine("=== Y-popup Diagnostic Report ===");
        sb.AppendLine($"Generated: {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("--- App Info ---");
        sb.AppendLine($"Version: {AppInfo.VersionDisplay}");
        sb.AppendLine();

        sb.AppendLine("--- OS Info ---");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Machine: {Environment.MachineName}");
        sb.AppendLine();

        sb.AppendLine("--- Settings ---");
        sb.AppendLine($"DisplayName: {settings.DisplayName}");
        sb.AppendLine($"MachineId: {settings.MachineId}");
        sb.AppendLine($"Group: {settings.Group}");
        sb.AppendLine($"PreferredLocalIp: {settings.PreferredLocalIp}");
        sb.AppendLine($"DiscoveryPort: {settings.DiscoveryPort}");
        sb.AppendLine($"TcpPort: {settings.TcpPort}");
        sb.AppendLine($"ShareFolderEnabled: {settings.ShareFolderEnabled}");
        sb.AppendLine($"ShareFolderPort: {settings.ShareFolderPort}");
        sb.AppendLine($"ShareFolderPath: {settings.ShareFolderPath}");
        sb.AppendLine($"ReceiveDirectory: {settings.ReceiveDirectory}");
        sb.AppendLine($"OnlySameGroup: {settings.OnlySameGroup}");
        sb.AppendLine();

        sb.AppendLine("--- Network Interfaces ---");
        var addresses = LocalNetworkHelper.GetLocalIPv4Addresses();
        sb.AppendLine($"Local IPv4: {(addresses.Count > 0 ? string.Join(", ", addresses) : "(none)")}");
        sb.AppendLine($"Resolved Preferred IP: {LocalNetworkHelper.ResolvePreferredIp(settings.PreferredLocalIp)}");

        var broadcasts = LocalNetworkHelper.GetLocalSubnetBroadcastAddresses();
        sb.AppendLine($"Broadcast targets: {(broadcasts.Count > 0 ? string.Join(", ", broadcasts) : "255.255.255.255")}");
        sb.AppendLine();

        sb.AppendLine("--- Port Availability ---");
        foreach (var portInfo in new[]
        {
            (Label: "Discovery (UDP)", Port: settings.DiscoveryPort, IsUdp: true),
            (Label: "TCP Message", Port: settings.TcpPort, IsUdp: false),
            (Label: "Share Folder (TCP)", Port: settings.ShareFolderPort, IsUdp: false)
        })
        {
            var status = portInfo.IsUdp ? CheckUdpPort(portInfo.Port) : CheckTcpPort(portInfo.Port);
            sb.AppendLine($"{portInfo.Label} ({portInfo.Port}): {status}");
        }
        sb.AppendLine();

        sb.AppendLine($"--- Peers ({peers.Count}) ---");
        foreach (var peer in peers)
        {
            var ago = now.ToUniversalTime() - peer.LastSeenUtc;
            sb.AppendLine($"  {peer.DisplayName} | {peer.IpAddress}:{peer.TcpPort} | {(int)ago.TotalSeconds}s ago | Group:{peer.Group}");
        }
        sb.AppendLine();

        if (logDirectory is not null)
        {
            sb.AppendLine("--- Recent Logs ---");
            var todayLog = Path.Combine(logDirectory, $"{now:yyyy-MM-dd}.log");
            if (File.Exists(todayLog))
            {
                try
                {
                    var lines = File.ReadLines(todayLog).Reverse().Take(30).Reverse();
                    foreach (var line in lines)
                    {
                        sb.AppendLine(line);
                    }
                }
                catch (Exception)
                {
                    sb.AppendLine("(cannot read log file)");
                }
            }
            else
            {
                sb.AppendLine("(no log file for today)");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=== End of Report ===");
        return sb.ToString();
    }

    private static string CheckTcpPort(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            socket.Bind(endpoint);
            return "Available";
        }
        catch (SocketException)
        {
            return "In use / blocked";
        }
    }

    private static string CheckUdpPort(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            socket.Bind(endpoint);
            return "Available";
        }
        catch (SocketException)
        {
            return "In use / blocked";
        }
    }
}
