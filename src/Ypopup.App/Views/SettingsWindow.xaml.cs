using System.Windows;
using System.Windows.Forms;
using Ypopup.App.Helpers;
using Ypopup.App.Services;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
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

        return preview;
    }

    private void RefreshFirewallStatus()
    {
        var preview = BuildFirewallSettingsPreview();
        var status = FirewallHelper.GetStatus(preview);
        FirewallStatusTextBlock.Text = FirewallHelper.GetStatusSummary(status, preview);
        FirewallExePathTextBlock.Text = string.IsNullOrWhiteSpace(status.ExecutablePath)
            ? string.Empty
            : $"실행 파일: {status.ExecutablePath}";
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
                            || discoveryPort != _coordinator.Settings.DiscoveryPort;

        _coordinator.SaveSettings(_workingSettings);

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
            AwayMessage = source.AwayMessage
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
