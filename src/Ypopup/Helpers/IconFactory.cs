using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ypopup.Helpers;

public static class IconFactory
{
    public static ImageSource CreateTrayIcon()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawEllipse(System.Windows.Media.Brushes.Firebrick, null, new System.Windows.Point(size / 2.0, size / 2.0), 14, 14);
            context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(14, 10, 4, 8));
            context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(10, 18, 12, 4));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
