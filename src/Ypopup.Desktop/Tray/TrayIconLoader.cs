using Avalonia.Controls;
using Avalonia.Platform;

namespace Ypopup.Desktop.Tray;

public static class TrayIconLoader
{
    public static WindowIcon LoadDefault()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Y-popup/Assets/tray.ico"));
        return new WindowIcon(stream);
    }
}
