using Avalonia.Controls;

namespace Ypopup.Desktop.Helpers;

/// <summary>
/// 모달을 띄운 동안 소유 창의 Topmost를 끄고, 그 사이 목록 갱신이 다시 켜지 못하게 합니다.
/// </summary>
public static class WindowDialogHelper
{
    private static int _suppressCount;

    public static bool IsTopmostSuppressed => _suppressCount > 0;

    public static async Task ShowDialogAsync(Window dialog, Window owner)
    {
        var isOutermost = _suppressCount == 0;
        var restore = new List<Window>();
        for (Window? window = owner; window is not null; window = window.Owner as Window)
        {
            if (!window.Topmost)
            {
                continue;
            }

            window.Topmost = false;
            restore.Add(window);
        }

        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _suppressCount++;
        try
        {
            await dialog.ShowDialog(owner);
        }
        finally
        {
            _suppressCount--;
            if (isOutermost)
            {
                foreach (var window in restore)
                {
                    window.Topmost = true;
                }
            }
        }
    }
}
