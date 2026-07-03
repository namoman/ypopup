using System.Windows;
using System.Windows.Input;
using Ypopup.Core.Models;
using Ypopup.Network;

namespace Ypopup.App.Views;

public partial class UserListWindow : Window
{
    private readonly YpopupCoordinator _coordinator;

    public UserListWindow(YpopupCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        Topmost = _coordinator.Settings.KeepWindowTopmost;
        UpdateDisplayName();
        RefreshPeers();
    }

    public void RefreshPeers()
    {
        Topmost = _coordinator.Settings.KeepWindowTopmost;
        UpdateDisplayName();
        ApplyFilter();
    }

    private void UpdateDisplayName()
    {
        DisplayNameTextBlock.Text = _coordinator.Settings.DisplayName;
    }

    private void ApplyFilter()
    {
        var text = SearchTextBox.Text.Trim();
        var allPeers = _coordinator.GetPeers();

        if (string.IsNullOrWhiteSpace(text))
        {
            PeerListBox.ItemsSource = allPeers;
        }
        else
        {
            PeerListBox.ItemsSource = allPeers.Where(p =>
                p.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.Group.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.IpAddress.Contains(text, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }

    private PeerInfo? GetSelectedPeer()
    {
        return PeerListBox.SelectedItem as PeerInfo;
    }

    private void OpenComposeWindow(PeerInfo peer)
    {
        var composeWindow = new ComposeWindow(_coordinator, peer);
        composeWindow.Show();
        composeWindow.Activate();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var peer = GetSelectedPeer();
        if (peer is null)
        {
            MessageBox.Show(this, "쪽지를 보낼 사용자를 선택하세요.", "Y-popup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OpenComposeWindow(peer);
    }

    private void PeerListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var peer = GetSelectedPeer();
        if (peer is not null)
        {
            OpenComposeWindow(peer);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPeers();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_coordinator);
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
        RefreshPeers();
    }

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ShareFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: PeerInfo peer })
        {
            return;
        }

        var window = new SharedFolderWindow(_coordinator, peer);
        window.Owner = this;
        window.Show();
        window.Activate();
    }
}
