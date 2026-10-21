using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Settings;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.ViewModels;
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
    private readonly HashSet<string> _avatarRequests = new(StringComparer.Ordinal);
    private Bitmap? _selfAvatarBitmap;

    public UserListWindow(YpopupCoordinator coordinator, WindowNavigator _)
    {
        InitializeComponent();
        _coordinator = coordinator;
        GroupTreeControl.DataContext = _groupTreeViewModel;
        GroupTreeControl.PeerDoubleTapped += peer => OpenCompose([peer], ComposeSendMode.Direct);
        GroupTreeControl.ShareFolderRequested += OpenShareFolder;
        GroupTreeControl.AvatarRequested += OnAvatarRequested;
        Closed += (_, _) =>
        {
            _selfAvatarBitmap?.Dispose();
            _groupTreeViewModel.ReleaseAvatars();
        };
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
        var settings = _coordinator.Settings;
        DisplayNameTextBlock.Text = FormatHeading(settings.DisplayName, settings.Group, settings.JobTitle);
        var away = _coordinator.IsAway;
        SelfStatusOnline.IsVisible = !away;
        SelfStatusAway.IsVisible = away;

        var bytes = AvatarStore.ReadBytes();
        Bitmap? next = bytes is { Length: > 0 } ? new Bitmap(new MemoryStream(bytes)) : null;
        SelfAvatarImage.Source = next;
        SelfAvatarImage.IsVisible = next is not null;
        SelfAvatarPlaceholder.IsVisible = next is null;
        var previous = _selfAvatarBitmap;
        _selfAvatarBitmap = next;
        previous?.Dispose();
    }

    private void ApplyFilter()
    {
        var checkedIds = GetCheckedPeers()
            .Select(peer => peer.MachineId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

        _groupTreeViewModel.UpdatePeers(filtered, _coordinator.TryGetCachedAvatar);
        RequestMissingAvatars(filtered);
        foreach (var member in _groupTreeViewModel.Groups.SelectMany(group => group.Members))
        {
            member.IsChecked = checkedIds.Contains(member.Peer.MachineId);
        }
    }

    private List<PeerInfo> GetCheckedPeers()
        => _groupTreeViewModel.Groups
            .SelectMany(group => group.Members)
            .Where(member => member.IsChecked)
            .Select(member => member.Peer)
            .ToList();

    private void OpenCompose(IReadOnlyList<PeerInfo> peers, ComposeSendMode mode)
    {
        var composeWindow = new ComposeWindow(_coordinator, peers, mode);
        composeWindow.Show();
        composeWindow.Activate();
    }

    private void RequestMissingAvatars(IReadOnlyList<PeerInfo> peers)
    {
        foreach (var peer in peers)
        {
            if (string.IsNullOrWhiteSpace(peer.AvatarHash))
            {
                continue;
            }

            if (!_avatarRequests.Add($"{peer.MachineId}|{peer.AvatarHash}"))
            {
                continue;
            }

            if (_coordinator.TryGetCachedAvatar(peer) is not null)
            {
                continue;
            }

            var captured = peer;
            _ = LoadAvatarAsync(captured);
        }
    }

    private async Task LoadAvatarAsync(PeerInfo peer)
    {
        var png = await _coordinator.GetPeerAvatarAsync(peer).ConfigureAwait(false);
        if (png is not { Length: > 0 })
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
            _groupTreeViewModel.ApplyAvatar(peer.MachineId, peer.AvatarHash, png));
    }

    private void OnAvatarRequested(PeerInfo peer)
    {
        _ = ShowPeerProfileAsync(peer);
    }

    private async Task ShowPeerProfileAsync(PeerInfo peer)
    {
        try
        {
            var png = _coordinator.TryGetCachedAvatar(peer) ?? await _coordinator.GetPeerAvatarAsync(peer);
            await PeerProfileWindow.ShowAsync(this, peer, png);
        }
        catch (Exception ex)
        {
            LogService.Error("UserList", $"프로필을 열지 못했습니다: {ex.Message}");
        }
    }

    private static string FormatHeading(string name, string group, string jobTitle)
    {
        var detail = string.Join("/", new[] { group, jobTitle }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrEmpty(detail) ? name : $"{name}({detail})";
    }

    private void SelfAvatar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            _ = ShowSelfProfileAsync();
        }
    }

    private async Task ShowSelfProfileAsync()
    {
        var settings = _coordinator.Settings;
        var self = new PeerInfo
        {
            MachineId = settings.MachineId,
            DisplayName = settings.DisplayName,
            IpAddress = LocalNetworkHelper.ResolvePreferredIp(settings.PreferredLocalIp),
            Group = settings.Group,
            JobTitle = settings.JobTitle,
            Email = settings.Email,
            Memo = settings.Memo,
            IsAway = _coordinator.IsAway
        };

        try
        {
            await PeerProfileWindow.ShowAsync(this, self, AvatarStore.ReadBytes());
        }
        catch (Exception ex)
        {
            LogService.Error("UserList", $"내 프로필을 열지 못했습니다: {ex.Message}");
        }
    }

    private void OpenShareFolder(PeerInfo peer)
    {
        var window = new SharedFolderWindow(_coordinator, peer);
        window.Show();
        window.Activate();
    }

    private async void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        var selected = GetCheckedPeers();
        if (selected.Count == 0)
        {
            await DialogHelper.ShowInfoAsync(this, "Y-popup", "쪽지를 보낼 사용자를 선택하세요.");
            return;
        }

        if (selected.Count == 1)
        {
            OpenCompose(selected, ComposeSendMode.Direct);
            return;
        }

        ShowSendMenu(selected);
    }

    private void ShowSendMenu(IReadOnlyList<PeerInfo> selected)
    {
        var note = new MenuItem { Header = $"쪽지 보내기 ({selected.Count}명)" };
        note.Click += (_, _) => OpenCompose(selected, ComposeSendMode.Direct);

        var room = new MenuItem { Header = "단톡방" };
        room.Click += async (_, _) => await CreateGroupRoomAsync(selected);

        var notice = new MenuItem { Header = "단체 쪽지" };
        notice.Click += (_, _) => OpenCompose(selected, ComposeSendMode.Notice);

        var flyout = new MenuFlyout
        {
            Placement = PlacementMode.TopEdgeAlignedLeft,
            Items = { note, room, notice }
        };
        flyout.ShowAt(SendButton);
    }

    private async Task CreateGroupRoomAsync(IReadOnlyList<PeerInfo> selected)
    {
        try
        {
            await _coordinator.CreateGroupRoomAndInviteAsync(BuildRoomTitle(selected), selected);
        }
        catch (Exception ex)
        {
            LogService.Error("UserList", $"단톡방 생성 실패: {ex.Message}");
            await DialogHelper.ShowErrorAsync(this, "Y-popup", $"단톡방을 만들지 못했습니다.\n\n{ex.Message}");
        }
    }

    private static string BuildRoomTitle(IReadOnlyList<PeerInfo> peers)
    {
        if (peers.Count <= 2)
        {
            return string.Join(", ", peers.Select(peer => peer.DisplayName));
        }

        return $"{peers[0].DisplayName}, {peers[1].DisplayName} 외 {peers.Count - 2}명";
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        _avatarRequests.Clear();
        RefreshPeers();
    }

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
