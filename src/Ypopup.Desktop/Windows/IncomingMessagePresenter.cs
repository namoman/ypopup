using Ypopup.Core.Models;
using Ypopup.Desktop.Platform.Notifications;
using Ypopup.Desktop.Views.Receive;
using Ypopup.Network;

namespace Ypopup.Desktop.Windows;

/// <summary>수신 쪽지 팝업과 알림음을 처리합니다.</summary>
public sealed class IncomingMessagePresenter
{
    private readonly YpopupCoordinator _coordinator;
    private readonly INotificationSoundService _notificationSound;

    public IncomingMessagePresenter(
        YpopupCoordinator coordinator,
        INotificationSoundService notificationSound)
    {
        _coordinator = coordinator;
        _notificationSound = notificationSound;
    }

    public void Present(ReceivedMessage message)
    {
        if (message.IsAutoReply)
        {
            return;
        }

        if (message.SavedFilePaths.Count > 0)
        {
            _notificationSound.PlayFileReceived(_coordinator.Settings);
        }
        else
        {
            _notificationSound.PlayMessageReceived(_coordinator.Settings);
        }

        var receiveWindow = new ReceiveWindow(_coordinator, message);
        receiveWindow.Show();
        receiveWindow.Activate();
    }
}
