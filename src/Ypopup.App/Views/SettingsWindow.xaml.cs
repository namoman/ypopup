using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using Ypopup.App.Helpers;
using Ypopup.App.Services;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Sharing;
using Ypopup.Network;

namespace Ypopup.App.Views;

public partial class SettingsWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private AppSettings _workingSettings = new();

    public SettingsWindow(YpopupCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        LoadSettings();
    }

    private void LoadSettings()
    {
        _workingSettings = CloneSettings(_coordinator.Settings);

        DisplayNameTextBox.Text = _workingSettings.DisplayName;
        GroupTextBox.Text = _workingSettings.Group;
        EmailTextBox.Text = _workingSettings.Email;
        MemoTextBox.Text = _workingSettings.Memo;

        LocalIpComboBox.ItemsSource = LocalNetworkHelper.GetLocalIPv4Addresses();
        LocalIpComboBox.Text = LocalNetworkHelper.ResolvePreferredIp(_workingSettings.PreferredLocalIp);
        DiscoveryPortTextBox.Text = _workingSettings.DiscoveryPort.ToString();
        TcpPortTextBox.Text = _workingSettings.TcpPort.ToString();
        OnlySameGroupCheckBox.IsChecked = _workingSettings.OnlySameGroup;

        KeepWindowTopmostCheckBox.IsChecked = _workingSettings.KeepWindowTopmost;
        RunAtStartupCheckBox.IsChecked = StartupRegistryService.IsEnabled();
        CloseComposeAfterSendCheckBox.IsChecked = _workingSettings.CloseComposeWindowAfterSend;
        CloseReceiveOnReplyCheckBox.IsChecked = _workingSettings.CloseReceiveWindowOnReply;
        SoundEnabledCheckBox.IsChecked = _workingSettings.SoundEnabled;
        PlayMessageSoundCheckBox.IsChecked = _workingSettings.PlayMessageReceivedSound;
        PlayFileSoundCheckBox.IsChecked = _workingSettings.PlayFileReceivedSound;
        ReceiveDirectoryTextBox.Text = _workingSettings.ReceiveDirectory;
        MessageFontHelper.ApplyPreview(_workingSettings, FontPreviewTextBlock);

        AwayIdleCheckBox.IsChecked = _workingSettings.AwayEnabledByIdle;
        AwayIdleMinutesTextBox.Text = _workingSettings.AwayIdleMinutes.ToString();
        AwayMessageTextBox.Text = _workingSettings.AwayMessage;

        ShareFolderEnabledCheckBox.IsChecked = _workingSettings.ShareFolderEnabled;
        ShareFolderPathTextBox.Text = _workingSettings.ShareFolderPath;
        ShareFolderPortTextBox.Text = _workingSettings.ShareFolderPort.ToString();

        RefreshFirewallStatus();
    }

    private void NetworkTab_GotFocus(object sender, RoutedEventArgs e)
    {
        RefreshFirewallStatus();
    }

    private AppSettings BuildFirewallSettingsPreview()
    {
        var preview = CloneSettings(_workingSettings);

        if (int.TryParse(DiscoveryPortTextBox.Text, out var discoveryPort))
        {
            preview.DiscoveryPort = discoveryPort;
        }

        if (int.TryParse(TcpPortTextBox.Text, out var tcpPort))
        {
            preview.TcpPort = tcpPort;
        }

        preview.ShareFolderEnabled = ShareFolderEnabledCheckBox.IsChecked == true;
        preview.ShareFolderPath = ShareFolderPathTextBox.Text.Trim();

        if (int.TryParse(ShareFolderPortTextBox.Text, out var shareFolderPort))
        {
            preview.ShareFolderPort = shareFolderPort;
        }

        return preview;
    }

    private void RefreshFirewallStatus()
    {
        var preview = BuildFirewallSettingsPreview();
        var status = FirewallHelper.GetStatus(preview);
        var summary = FirewallHelper.GetStatusSummary(status, preview);
        summary += "\n" + BuildShareFolderHostStatusText(preview);
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

        var hostStatus = _coordinator.SharedFolderHostStatus;
        if (hostStatus.IsRunning)
        {
            return $"공유폴더 서버: 실행 중 (TCP *:{preview.ShareFolderPort})";
        }

        return $"공유폴더 서버: 중지됨 ({hostStatus.ErrorMessage ?? "알 수 없음"})";
    }

    private void AddFirewallRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TcpPortTextBox.Text, out var tcpPort) || tcpPort is < 1024 or > 65535)
        {
            MessageBox.Show(this, "TCP 포트는 1024~65535 사이여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(DiscoveryPortTextBox.Text, out var discoveryPort) || discoveryPort is < 1024 or > 65535)
        {
            MessageBox.Show(this, "UDP 포트는 1024~65535 사이여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (tcpPort == discoveryPort)
        {
            MessageBox.Show(this, "TCP 포트와 UDP 포트는 다른 번호여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var preview = BuildFirewallSettingsPreview();
        if (FirewallHelper.TryAddFirewallRules(preview, out var message))
        {
            MessageBox.Show(this, message, "Y-popup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(this, message, "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        RefreshFirewallStatus();
    }

    private void OpenFirewallSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FirewallHelper.OpenWindowsFirewallSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"방화벽 설정을 열 수 없습니다.\n\n{ex.Message}", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshFirewallStatusButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshFirewallStatus();
    }

    private void ChangeFontButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FontDialog
        {
            Font = new Font(_workingSettings.MessageFontFamily, (float)_workingSettings.MessageFontSize)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _workingSettings.MessageFontFamily = dialog.Font.FontFamily.Name;
            _workingSettings.MessageFontSize = dialog.Font.Size;
            MessageFontHelper.ApplyPreview(_workingSettings, FontPreviewTextBlock);
        }
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "수신 파일 저장 폴더 선택",
            SelectedPath = ReceiveDirectoryTextBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ReceiveDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OpenShareFolderInExplorerButton_Click(object sender, RoutedEventArgs e)
    {
        var path = ShareFolderPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            path = SharedFolderPathHelper.GetDefaultShareFolderPath();
            ShareFolderPathTextBox.Text = path;
        }

        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"탐색기를 열 수 없습니다.\n\n{ex.Message}", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BrowseShareFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "공유할 폴더 선택",
            SelectedPath = ShareFolderPathTextBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ShareFolderPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TcpPortTextBox.Text, out var tcpPort) || tcpPort is < 1024 or > 65535)
        {
            MessageBox.Show(this, "TCP 포트는 1024~65535 사이여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(DiscoveryPortTextBox.Text, out var discoveryPort) || discoveryPort is < 1024 or > 65535)
        {
            MessageBox.Show(this, "UDP 포트는 1024~65535 사이여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (tcpPort == discoveryPort)
        {
            MessageBox.Show(this, "TCP 포트와 UDP 포트는 다른 번호여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var shareFolderEnabled = ShareFolderEnabledCheckBox.IsChecked == true;
        var shareFolderPort = AppConstants.DefaultShareFolderPort;
        if (shareFolderEnabled)
        {
            if (!int.TryParse(ShareFolderPortTextBox.Text, out shareFolderPort) || shareFolderPort is < 1024 or > 65535)
            {
                MessageBox.Show(this, "공유폴더 HTTP 포트는 1024~65535 사이여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (shareFolderPort == tcpPort || shareFolderPort == discoveryPort)
            {
                MessageBox.Show(this, "공유폴더 포트는 TCP/UDP 포트와 다른 번호여야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ShareFolderPathTextBox.Text))
            {
                MessageBox.Show(this, "공유폴더 경로를 입력하세요.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        int awayIdleMinutes = 10;
        if (AwayIdleCheckBox.IsChecked == true)
        {
            if (!int.TryParse(AwayIdleMinutesTextBox.Text, out awayIdleMinutes) || awayIdleMinutes < 1)
            {
                MessageBox.Show(this, "부재 유휴 시간은 1분 이상이어야 합니다.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            MessageBox.Show(this, "표시 이름을 입력하세요.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _workingSettings.DisplayName = DisplayNameTextBox.Text.Trim();
        _workingSettings.Group = GroupTextBox.Text.Trim();
        _workingSettings.Email = EmailTextBox.Text.Trim();
        _workingSettings.Memo = MemoTextBox.Text.Trim();
        _workingSettings.PreferredLocalIp = LocalIpComboBox.Text.Trim();
        _workingSettings.DiscoveryPort = discoveryPort;
        _workingSettings.TcpPort = tcpPort;
        _workingSettings.OnlySameGroup = OnlySameGroupCheckBox.IsChecked == true;
        _workingSettings.KeepWindowTopmost = KeepWindowTopmostCheckBox.IsChecked == true;
        _workingSettings.CloseComposeWindowAfterSend = CloseComposeAfterSendCheckBox.IsChecked == true;
        _workingSettings.CloseReceiveWindowOnReply = CloseReceiveOnReplyCheckBox.IsChecked == true;
        _workingSettings.SoundEnabled = SoundEnabledCheckBox.IsChecked == true;
        _workingSettings.PlayMessageReceivedSound = PlayMessageSoundCheckBox.IsChecked == true;
        _workingSettings.PlayFileReceivedSound = PlayFileSoundCheckBox.IsChecked == true;
        _workingSettings.ReceiveDirectory = ReceiveDirectoryTextBox.Text.Trim();
        _workingSettings.AwayEnabledByIdle = AwayIdleCheckBox.IsChecked == true;
        _workingSettings.AwayIdleMinutes = awayIdleMinutes;
        _workingSettings.AwayMessage = AwayMessageTextBox.Text.Trim();
        _workingSettings.ShareFolderEnabled = shareFolderEnabled;
        _workingSettings.ShareFolderPath = ShareFolderPathTextBox.Text.Trim();
        _workingSettings.ShareFolderPort = shareFolderPort;

        try
        {
            StartupRegistryService.SetEnabled(RunAtStartupCheckBox.IsChecked == true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"시작 프로그램 등록에 실패했습니다.\n\n{ex.Message}", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var requiresRestart = tcpPort != _coordinator.Settings.TcpPort
                            || discoveryPort != _coordinator.Settings.DiscoveryPort
                            || shareFolderPort != _coordinator.Settings.ShareFolderPort;

        _coordinator.SaveSettings(_workingSettings);

        if (_workingSettings.ShareFolderEnabled)
        {
            RefreshFirewallStatus();
            if (!_coordinator.SharedFolderHostStatus.IsRunning)
            {
                MessageBox.Show(
                    this,
                    $"공유폴더 서버를 시작하지 못했습니다.\n\n{_coordinator.SharedFolderHostStatus.ErrorMessage}",
                    "Y-popup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        var message = requiresRestart
            ? "설정이 저장되었습니다.\n포트 변경은 프로그램 재시작 후 적용됩니다."
            : "설정이 저장되었습니다.";

        MessageBox.Show(this, message, "Y-popup", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        return new AppSettings
        {
            MachineId = source.MachineId,
            DisplayName = source.DisplayName,
            PreferredLocalIp = source.PreferredLocalIp,
            Group = source.Group,
            OnlySameGroup = source.OnlySameGroup,
            Email = source.Email,
            Memo = source.Memo,
            KeepWindowTopmost = source.KeepWindowTopmost,
            CloseComposeWindowAfterSend = source.CloseComposeWindowAfterSend,
            CloseReceiveWindowOnReply = source.CloseReceiveWindowOnReply,
            SoundEnabled = source.SoundEnabled,
            PlayMessageReceivedSound = source.PlayMessageReceivedSound,
            PlayFileReceivedSound = source.PlayFileReceivedSound,
            MessageFontFamily = source.MessageFontFamily,
            MessageFontSize = source.MessageFontSize,
            ReceiveDirectory = source.ReceiveDirectory,
            TcpPort = source.TcpPort,
            DiscoveryPort = source.DiscoveryPort,
            AwayEnabledByIdle = source.AwayEnabledByIdle,
            AwayIdleMinutes = source.AwayIdleMinutes,
            AwayMessage = source.AwayMessage,
            ShareFolderEnabled = source.ShareFolderEnabled,
            ShareFolderPath = source.ShareFolderPath,
            ShareFolderPort = source.ShareFolderPort
        };
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open link: {ex.Message}");
        }
    }
}
