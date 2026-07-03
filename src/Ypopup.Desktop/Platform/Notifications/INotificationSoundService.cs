using Ypopup.Core.Models;

namespace Ypopup.Desktop.Platform.Notifications;

public interface INotificationSoundService
{
    void PlayMessageReceived(AppSettings settings);

    void PlayFileReceived(AppSettings settings);
}
