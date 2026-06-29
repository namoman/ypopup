using System.Windows;
using Ypopup.Models;
using Ypopup.Services;

namespace Ypopup.Views;

public partial class SettingsWindow : Window
{
    private readonly YpopupCoordinator _coordinator;

    public SettingsWindow(YpopupCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _coordinator.Settings;
        DisplayNameTextBox.Text = settings.DisplayName;
        ReceiveDirectoryTextBox.Text = settings.ReceiveDirectory;
        TcpPortTextBox.Text = settings.TcpPort.ToString();
        DiscoveryPortTextBox.Text = settings.DiscoveryPort.ToString();
        PlaySoundCheckBox.IsChecked = settings.PlayNotificationSound;
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
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
            MessageBox.Show(this, "TCP 포트는 1024~65535 사이여야 합니다.", "Ypopup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(DiscoveryPortTextBox.Text, out var discoveryPort) || discoveryPort is < 1024 or > 65535)
        {
            MessageBox.Show(this, "탐색 포트는 1024~65535 사이여야 합니다.", "Ypopup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            MessageBox.Show(this, "표시 이름을 입력하세요.", "Ypopup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = new AppSettings
        {
            MachineId = _coordinator.Settings.MachineId,
            DisplayName = DisplayNameTextBox.Text.Trim(),
            ReceiveDirectory = ReceiveDirectoryTextBox.Text.Trim(),
            TcpPort = tcpPort,
            DiscoveryPort = discoveryPort,
            PlayNotificationSound = PlaySoundCheckBox.IsChecked == true
        };

        _coordinator.SaveSettings(settings);
        MessageBox.Show(
            this,
            "설정이 저장되었습니다.\n포트 변경은 프로그램 재시작 후 적용됩니다.",
            "Ypopup",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
