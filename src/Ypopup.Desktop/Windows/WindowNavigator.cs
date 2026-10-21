using Avalonia.Controls;
using Avalonia.Threading;
using Ypopup.Core.Models;
using Ypopup.Desktop.Helpers;
using Ypopup.Desktop.ViewModels;
using Ypopup.Desktop.Views.About;
using Ypopup.Desktop.Views.Chat;
using Ypopup.Desktop.Views.Diagnostics;
using Ypopup.Desktop.Views.Settings;
using Ypopup.Desktop.Views.UserList;
using Ypopup.Network;

namespace Ypopup.Desktop.Windows;

/// <summary>앱 창(사용자 목록·설정·정보·단톡방) 표시를 담당합니다.</summary>
public sealed class WindowNavigator
{
    private readonly YpopupCoordinator _coordinator;
    private readonly ChatWindowManager _chatWindowManager;
    private UserListWindow? _userListWindow;

    public WindowNavigator(YpopupCoordinator coordinator)
    {
        _coordinator = coordinator;
        _chatWindowManager = new ChatWindowManager(CreateChatRoomWindow);
        _coordinator.GroupRoomOpened += room =>
            Dispatcher.UIThread.Post(() => OpenGroupRoom(room));
        _coordinator.ParticipantLeft += (roomId, userId, name) =>
            Dispatcher.UIThread.Post(() =>
            {
                if (_chatWindowManager.GetRoomWindow(roomId)?.DataContext is ChatSessionViewModel vm)
                {
                    vm.NoteParticipantLeft(userId, name);
                }
            });
    }

    public void RefreshUserListIfOpen()
    {
        _userListWindow?.RefreshPeers();
    }

    public void ShowUserList()
    {
        if (_userListWindow is { IsVisible: true })
        {
            if (_userListWindow.WindowState == WindowState.Minimized)
            {
                _userListWindow.WindowState = WindowState.Normal;
            }

            _userListWindow.Activate();
            return;
        }

        _userListWindow = new UserListWindow(_coordinator, this);
        _userListWindow.Closed += (_, _) => _userListWindow = null;
        _userListWindow.Show();
        _userListWindow.Activate();
    }

    public void OpenGroupRoom(ChatRoom room)
    {
        _chatWindowManager.OpenRoomWindow(room.RoomId, room);
    }

    private Window CreateChatRoomWindow(ChatRoom room)
    {
        var viewModel = new ChatSessionViewModel(
            room,
            _coordinator.Settings.MachineId,
            _coordinator.Settings.DisplayName,
            _coordinator.MessageDispatcher,
            _coordinator.ChatStorage,
            () => _coordinator.GetPeerUsers());

        _coordinator.PacketRouter.RegisterRoomHandler(room.RoomId, packet =>
        {
            if (packet.Type != ExtendedPacketType.GroupChatMessage)
            {
                return;
            }

            var msg = new ChatMessage(
                MessageId: packet.MessageId,
                RoomId: packet.RoomId ?? room.RoomId,
                SenderId: packet.SenderId,
                SenderName: packet.SenderName,
                Content: packet.Body,
                Type: MessageType.Text,
                TimestampUtc: packet.SentAtUtc == default ? DateTime.UtcNow : packet.SentAtUtc);

            _ = _coordinator.ChatStorage.AppendMessageAsync(msg);
            viewModel.AddIncomingMessage(msg);
        });

        Func<Task>? leaveRoom = room.Type == RoomType.GroupChat
            ? () => _coordinator.LeaveGroupRoomAsync(room.RoomId)
            : null;
        var window = new ChatRoomWindow(viewModel, leaveRoom);
        window.Closed += (_, _) => _coordinator.PacketRouter.UnregisterRoomHandler(room.RoomId);
        return window;
    }

    public async Task ShowSettingsAsync()
    {
        try
        {
            var settingsWindow = new SettingsWindow(_coordinator);
            if (_userListWindow is { IsVisible: true } owner)
            {
                await WindowDialogHelper.ShowDialogAsync(settingsWindow, owner);
            }
            else
            {
                settingsWindow.Show();
                settingsWindow.Activate();
            }

            RefreshUserListIfOpen();
        }
        catch (Exception ex)
        {
            if (_userListWindow is { IsVisible: true } owner)
            {
                await DialogHelper.ShowErrorAsync(owner, "Y-popup", $"설정 창을 열 수 없습니다.\n\n{ex.Message}");
            }
        }
    }

    public async Task ShowAboutAsync()
    {
        var aboutWindow = new AboutWindow();
        if (_userListWindow is { IsVisible: true } owner)
        {
            await WindowDialogHelper.ShowDialogAsync(aboutWindow, owner);
        }
        else
        {
            aboutWindow.Show();
            aboutWindow.Activate();
        }
    }

    public async Task ShowLanDiagnosticAsync()
    {
        var diagWindow = new LanDiagnosticWindow(_coordinator);
        if (_userListWindow is { IsVisible: true } owner)
        {
            await WindowDialogHelper.ShowDialogAsync(diagWindow, owner);
        }
        else
        {
            diagWindow.Show();
            diagWindow.Activate();
        }
    }
}
