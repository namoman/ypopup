using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Core.Diagnostics;
using Ypopup.Core.Network;
using Ypopup.Desktop.Helpers;
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

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var settings = _coordinator.Settings;
            var peers = _coordinator.GetPeers();

            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Y-popup", "logs");

            var report = DiagnosticExporter.Generate(settings, peers, logDir);

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var filePath = Path.Combine(desktop, $"Y-popup-diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(filePath, report);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });

            await DialogHelper.ShowInfoAsync(this, "Y-popup", $"진단 정보를 내보냈습니다.\n\n{filePath}");
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(this, "Y-popup", $"진단 내보내기 실패:\n\n{ex.Message}");
        }
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e) => RefreshDiagnostics();

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
