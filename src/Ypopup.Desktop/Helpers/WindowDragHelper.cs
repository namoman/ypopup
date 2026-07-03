using Avalonia.Controls;
using Avalonia.Input;

namespace Ypopup.Desktop.Helpers;

public static class WindowDragHelper
{
    public static void OnTitleBarPointerPressed(Window window, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
        {
            window.BeginMoveDrag(e);
        }
    }
}
