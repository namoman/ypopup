using System.Windows;
using System.Windows.Input;
using Ypopup.Models;
using Ypopup.Services;

namespace Ypopup.Views;

public partial class UserListWindow : Window
{
    private readonly YpopupCoordinator _coordinator;

    public UserListWindow(YpopupCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        RefreshPeers();
    }

    public void RefreshPeers()
    {
        PeerListBox.ItemsSource = _coordinator.GetPeers();
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
            MessageBox.Show(this, "쪽지를 보낼 사용자를 선택하세요.", "Ypopup", MessageBoxButton.OK, MessageBoxImage.Information);
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
}
