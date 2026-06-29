using System.Drawing;
using System.Windows;

namespace Ypopup.App.Helpers;

public static class IconFactory
{
    private const string TrayIconResourcePath = "pack://application:,,,/Assets/tray.ico";

    public static Icon CreateTrayIcon()
    {
        try
        {
            var streamInfo = Application.GetResourceStream(new Uri(TrayIconResourcePath, UriKind.Absolute))
                             ?? throw new InvalidOperationException("트레이 아이콘 리소스를 찾을 수 없습니다.");

            using var stream = streamInfo.Stream;
            using var icon = new Icon(stream);
            return (Icon)icon.Clone();
        }
        catch (Exception)
        {
            return CreateFallbackIcon();
        }
    }

    private static Icon CreateFallbackIcon()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(System.Drawing.Color.SteelBlue);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(handle);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
