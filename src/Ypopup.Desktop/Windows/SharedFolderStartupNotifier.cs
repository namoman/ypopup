using Ypopup.Desktop.Helpers;
using Ypopup.Network;

namespace Ypopup.Desktop.Windows;

/// <summary>공유폴더 호스트 시작 실패 시 사용자에게 안내합니다.</summary>
public static class SharedFolderStartupNotifier
{
    public static async Task NotifyIfFailedAsync(YpopupCoordinator coordinator)
    {
        if (!coordinator.Settings.ShareFolderEnabled)
        {
            return;
        }

        var status = coordinator.SharedFolderHostStatus;
        if (status.IsRunning)
        {
            return;
        }

        var port = coordinator.Settings.ShareFolderPort;
        await DialogHelper.ShowWarningAsync(
            null,
            "Y-popup",
            $"공유폴더 서버를 시작하지 못했습니다.\n\n{status.ErrorMessage}\n\n" +
            $"설정 > 일반에서 공유폴더 사용을 확인하고, 설정 > 네트워크 > 방화벽에서 TCP {port} 허용을 추가하세요.\n" +
            "공유할 파일은 exe 옆 share 폴더에 넣으세요.");
    }
}
