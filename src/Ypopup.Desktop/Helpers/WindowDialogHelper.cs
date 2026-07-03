using Avalonia.Controls;

namespace Ypopup.Desktop.Helpers;

/// <summary>Topmost 부모 창 위에 모달 대화상자를 안정적으로 표시합니다.</summary>
public static class WindowDialogHelper
{
    public static async Task ShowDialogAsync(Window dialog, Window owner)
    {
        var restoreOwnerTopmost = owner.Topmost;
        if (restoreOwnerTopmost)
        {
            owner.Topmost = false;
        }

        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.Topmost = true;

        try
        {
            await dialog.ShowDialog(owner);
        }
        finally
        {
            if (restoreOwnerTopmost)
            {
                owner.Topmost = true;
            }
        }
    }
}
