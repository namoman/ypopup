using Avalonia;
using Avalonia.Media.Imaging;

namespace Ypopup.Desktop.Helpers;

public static class AvatarImage
{
    public const int MaxEdge = 96;
    public const long MaxSourceBytes = 20 * 1024 * 1024;

    public static byte[] CreateThumbnailPng(string sourcePath)
    {
        var length = new FileInfo(sourcePath).Length;
        if (length <= 0 || length > MaxSourceBytes)
        {
            throw new InvalidDataException("그림 파일이 너무 큽니다. 20MB 이하로 골라 주세요.");
        }

        using var original = new Bitmap(sourcePath);
        var size = original.PixelSize;
        var longest = Math.Max(size.Width, size.Height);
        if (longest <= MaxEdge)
        {
            return SavePng(original);
        }

        var scale = (double)MaxEdge / longest;
        var destination = new PixelSize(
            Math.Max(1, (int)Math.Round(size.Width * scale)),
            Math.Max(1, (int)Math.Round(size.Height * scale)));
        using var scaled = original.CreateScaledBitmap(destination);
        return SavePng(scaled);
    }

    private static byte[] SavePng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }
}
