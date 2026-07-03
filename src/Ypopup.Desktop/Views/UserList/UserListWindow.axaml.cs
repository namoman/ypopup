using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ypopup.Core.Models;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.Views.Compose;
using Ypopup.Desktop.Views.Settings;
using Ypopup.Desktop.Views.SharedFolder;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.UserList;

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
        UpdateSearchPlaceholder();
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
        var text = SearchTextBox.Text?.Trim() ?? string.Empty;
        var peers = _coordinator.GetPeers()
            .OrderBy(peer => peer.DisplayName, StringComparer.CurrentCultureIgnoreCase);

        PeerListBox.ItemsSource = string.IsNullOrWhiteSpace(text)
            ? peers.ToList()
            : peers.Where(p =>
                p.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.Group.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.IpAddress.Contains(text, StringComparison.OrdinalIgnoreCase)
            ).ToList();
    }

    private PeerInfo? GetSelectedPeer() => PeerListBox.SelectedItem as PeerInfo;

    private void OpenComposeWindow(PeerInfo peer)
    {
        var composeWindow = new ComposeWindow(_coordinator, peer);
        composeWindow.Show();
        composeWindow.Activate();
    }

    private async void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        var peer = GetSelectedPeer();
        if (peer is null)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "쪽지를 보낼 사용자를 선택하세요.");
            return;
        }

        OpenComposeWindow(peer);
    }

    private void PeerListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var peer = GetSelectedPeer();
        if (peer is not null)
        {
            OpenComposeWindow(peer);
        }
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e) => RefreshPeers();

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        => WindowDragHelper.OnTitleBarPointerPressed(this, e);

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = new SettingsWindow(_coordinator);
            await WindowDialogHelper.ShowDialogAsync(settingsWindow, this);
            RefreshPeers();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(this, "Y-popup", $"설정 창을 열 수 없습니다.\n\n{ex.Message}");
        }
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
        UpdateSearchPlaceholder();
    }

    private void UpdateSearchPlaceholder()
    {
        SearchPlaceholderTextBlock.IsVisible = string.IsNullOrEmpty(SearchTextBox.Text);
    }

    private void ShareFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PeerInfo peer })
        {
            return;
        }

        var window = new SharedFolderWindow(_coordinator, peer);
        window.Show();
        window.Activate();
    }
}
