using Avalonia.Controls;
using Avalonia.Interactivity;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Settings;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.Platform.Firewall;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.Settings.Panels;

public partial class NetworkSettingsPanel : UserControl
{
    private IFirewallService _firewallService = FirewallServiceFactory.Create();
    private YpopupCoordinator? _coordinator;
    private AppSettings _previewSettings = new();
    private Func<AppSettings>? _buildPreviewSettings;

    public NetworkSettingsPanel()
    {
        InitializeComponent();
    }

    public void Initialize(
        IFirewallService firewallService,
        YpopupCoordinator coordinator,
        Func<AppSettings> buildPreviewSettings)
    {
        _firewallService = firewallService;
        _coordinator = coordinator;
        _buildPreviewSettings = buildPreviewSettings;
    }

    public void Load(AppSettings settings)
    {
        var addresses = LocalNetworkHelper.GetLocalIPv4Addresses().ToList();
        var resolved = LocalNetworkHelper.ResolvePreferredIp(settings.PreferredLocalIp);
        LocalIpComboBox.ItemsSource = addresses;
        LocalIpComboBox.Text = resolved;

        DiscoveryPortTextBox.Text = settings.DiscoveryPort.ToString();
        TcpPortTextBox.Text = settings.TcpPort.ToString();
        ShareFolderPortTextBox.Text = settings.ShareFolderPort.ToString();
        OnlySameGroupCheckBox.IsChecked = settings.OnlySameGroup;
        _previewSettings = settings;
        RefreshFirewallStatus();
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.PreferredLocalIp = LocalIpComboBox.Text?.Trim() ?? string.Empty;

        if (int.TryParse(DiscoveryPortTextBox.Text, out var discoveryPort))
        {
            settings.DiscoveryPort = discoveryPort;
        }

        if (int.TryParse(TcpPortTextBox.Text, out var tcpPort))
        {
            settings.TcpPort = tcpPort;
        }

        if (int.TryParse(ShareFolderPortTextBox.Text, out var shareFolderPort))
        {
            settings.ShareFolderPort = shareFolderPort;
        }

        settings.OnlySameGroup = OnlySameGroupCheckBox.IsChecked == true;
    }

    public string DiscoveryPortText => DiscoveryPortTextBox.Text ?? string.Empty;
    public string TcpPortText => TcpPortTextBox.Text ?? string.Empty;
    public string ShareFolderPortText => ShareFolderPortTextBox.Text ?? string.Empty;

    public void RefreshFirewallStatus()
    {
        if (_buildPreviewSettings is not null)
        {
            _previewSettings = _buildPreviewSettings();
        }

        var status = _firewallService.GetStatus(_previewSettings);
        var summary = _firewallService.GetStatusSummary(status, _previewSettings);
        summary += "\n" + BuildShareFolderHostStatusText(_previewSettings);
        FirewallStatusTextBlock.Text = summary;
        FirewallExePathTextBlock.Text = string.IsNullOrWhiteSpace(status.ExecutablePath)
            ? string.Empty
            : $"실행 파일: {status.ExecutablePath}";
    }

    private string BuildShareFolderHostStatusText(AppSettings preview)
    {
        if (!preview.ShareFolderEnabled)
        {
            return "공유폴더 서버: 사용 안 함";
        }

        var hostStatus = _coordinator?.SharedFolderHostStatus;
        if (hostStatus?.IsRunning == true)
        {
            return $"공유폴더 서버: 실행 중 (TCP *:{preview.ShareFolderPort})";
        }

        return $"공유폴더 서버: 중지됨 ({hostStatus?.ErrorMessage ?? "알 수 없음"})";
    }

    private Window? GetOwnerWindow() => TopLevel.GetTopLevel(this) as Window;

    private async void AddFirewallRuleButton_Click(object? sender, RoutedEventArgs e)
    {
        var owner = GetOwnerWindow();
        var tcpResult = SettingsValidator.ValidatePort(TcpPortTextBox.Text, "TCP");
        if (!tcpResult.IsValid)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", tcpResult.ErrorMessage);
            return;
        }

        var discoveryResult = SettingsValidator.ValidatePort(DiscoveryPortTextBox.Text, "UDP");
        if (!discoveryResult.IsValid)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", discoveryResult.ErrorMessage);
            return;
        }

        var tcpPort = int.Parse(TcpPortTextBox.Text!);
        var discoveryPort = int.Parse(DiscoveryPortTextBox.Text!);
        var portsDifferResult = SettingsValidator.ValidatePortsDiffer(tcpPort, discoveryPort, "TCP", "UDP");
        if (!portsDifferResult.IsValid)
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", portsDifferResult.ErrorMessage);
            return;
        }

        var preview = _buildPreviewSettings?.Invoke() ?? _previewSettings;
        if (_firewallService.TryAddFirewallRules(preview, out var message))
        {
            await DialogHelper.ShowInfoAsync(owner, "Y-popup", message);
        }
        else
        {
            await DialogHelper.ShowWarningAsync(owner, "Y-popup", message);
        }

        RefreshFirewallStatus();
    }

    private async void OpenFirewallSettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _firewallService.OpenFirewallSettings();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowWarningAsync(GetOwnerWindow(), "Y-popup", $"방화벽 설정을 열 수 없습니다.\n\n{ex.Message}");
        }
    }

    private void RefreshFirewallStatusButton_Click(object? sender, RoutedEventArgs e)
        => RefreshFirewallStatus();
}
