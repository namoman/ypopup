using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Core.Network;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.Diagnostics;

public partial class LanDiagnosticWindow : Window
{
    private readonly YpopupCoordinator _coordinator;

    public LanDiagnosticWindow(YpopupCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        RefreshDiagnostics();
    }

    private void RefreshDiagnostics()
    {
        var settings = _coordinator.Settings;
        var now = DateTime.UtcNow;

        LocalIpTextBlock.Text = LocalNetworkHelper.ResolvePreferredIp(settings.PreferredLocalIp);

        var broadcasts = LocalNetworkHelper.GetLocalSubnetBroadcastAddresses();
        BroadcastTextBlock.Text = broadcasts.Count > 0
            ? string.Join(", ", broadcasts)
            : "255.255.255.255";

        DiscoveryPortTextBlock.Text = settings.DiscoveryPort.ToString();
        TcpPortTextBlock.Text = settings.TcpPort.ToString();

        LastAnnounceTextBlock.Text = _coordinator.LastAnnounceSentUtc == default
            ? "없음"
            : FormatElapsed(now - _coordinator.LastAnnounceSentUtc);
        LastPacketTextBlock.Text = _coordinator.LastPacketReceivedUtc == default
            ? "없음"
            : FormatElapsed(now - _coordinator.LastPacketReceivedUtc);

        var peers = _coordinator.GetPeers();
        PeerCountTextBlock.Text = $"{peers.Count}명";
        PeerListControl.ItemsSource = peers;
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 1)
        {
            return "방금 전";
        }

        if (elapsed.TotalSeconds < 60)
        {
            return $"{(int)elapsed.TotalSeconds}초 전";
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"{(int)elapsed.TotalMinutes}분 전";
        }

        return $">1시간 전";
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e) => RefreshDiagnostics();

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
