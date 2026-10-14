using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ypopup.Core.Models;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.ViewModels;
using Ypopup.Desktop.Views.Chat;
using Ypopup.Desktop.Views.Compose;
using Ypopup.Desktop.Views.Settings;
using Ypopup.Desktop.Views.SharedFolder;
using Ypopup.Desktop.Windows;
using Ypopup.Network;

namespace Ypopup.Desktop.Views.UserList;

public partial class UserListWindow : Window
{
    private readonly YpopupCoordinator _coordinator;
    private readonly UserGroupTreeViewModel _groupTreeViewModel = new();
    private PeerInfo? _selectedPeer;

    public UserListWindow(YpopupCoordinator coordinator, WindowNavigator _)
    {
        InitializeComponent();
        _coordinator = coordinator;
        GroupTreeControl.DataContext = _groupTreeViewModel;
        GroupTreeControl.PeerSelected += peer => _selectedPeer = peer;
        GroupTreeControl.PeerDoubleTapped += OpenComposeWindow;
        GroupTreeControl.ShareFolderRequested += OpenShareFolder;
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

        var filtered = string.IsNullOrWhiteSpace(text)
            ? peers.ToList()
            : peers.Where(p =>
                p.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.Group.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                p.IpAddress.Contains(text, StringComparison.OrdinalIgnoreCase)
            ).ToList();

        _groupTreeViewModel.UpdatePeers(filtered);
    }

    private void OpenComposeWindow(PeerInfo peer)
    {
        var composeWindow = new ComposeWindow(_coordinator, peer);
        composeWindow.Show();
        composeWindow.Activate();
    }

    private void OpenShareFolder(PeerInfo peer)
    {
        var window = new SharedFolderWindow(_coordinator, peer);
        window.Show();
        window.Activate();
    }

    private async void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedPeer is null)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "쪽지를 보낼 사용자를 선택하세요.");
            return;
        }

        OpenComposeWindow(_selectedPeer);
    }

    private async void CreateRoomButton_Click(object? sender, RoutedEventArgs e)
    {
        var peers = _coordinator.GetPeers().ToList();
        if (peers.Count == 0)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "초대할 LAN 사용자가 없습니다.");
            return;
        }

        var dialog = new CreateRoomDialog(peers);
        await dialog.ShowDialog(this);
        if (dialog.Result is null || dialog.Result.SelectedPeers.Count == 0)
        {
            return;
        }

        try
        {
            await _coordinator.CreateGroupRoomAndInviteAsync(
                dialog.Result.Title,
                dialog.Result.SelectedPeers);
            // GroupRoomOpened 이벤트로 채팅창 오픈
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(this, "Y-popup", $"단톡방을 만들지 못했습니다.\n\n{ex.Message}");
        }
    }

    private async void MulticastButton_Click(object? sender, RoutedEventArgs e)
    {
        var peers = _coordinator.GetPeers().ToList();
        if (peers.Count == 0)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "보낼 LAN 사용자가 없습니다.");
            return;
        }

        var dialog = new CreateRoomDialog(peers)
        {
            Title = "단체 쪽지 보내기"
        };
        await dialog.ShowDialog(this);
        if (dialog.Result is null || dialog.Result.SelectedPeers.Count == 0)
        {
            return;
        }

        var body = dialog.Result.Body;
        if (string.IsNullOrWhiteSpace(body))
        {
            body = dialog.Result.Title;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "단체 쪽지 내용을 입력하세요.");
            return;
        }

        try
        {
            var results = await _coordinator.SendMulticastNoticeAsync(body, dialog.Result.SelectedPeers);
            var ok = results.Count(r => r.IsSuccess);
            await DialogHelper.ShowInfoAsync(
                this,
                "Y-popup",
                $"단체 쪽지 전송 완료: 성공 {ok} / 전체 {results.Count}");
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(this, "Y-popup", $"단체 쪽지 전송 실패.\n\n{ex.Message}");
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
}
