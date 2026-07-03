using Avalonia.Controls;

namespace Ypopup.Desktop.Tray;

public sealed class TrayIconManager : IDisposable
{
    private TrayIcon? _trayIcon;

    public void Show(string toolTipText, NativeMenu menu, Action onClicked)
    {
        Dispose();

        _trayIcon = new TrayIcon
        {
            Icon = TrayIconLoader.LoadDefault(),
            ToolTipText = toolTipText,
            IsVisible = true,
            Menu = menu
        };
        _trayIcon.Clicked += (_, _) => onClicked();
    }

    public void Dispose()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
    }
}
