using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var sourcePath = Path.Combine(projectRoot, "ref", "icon.png");
var assetsDir = Path.Combine(projectRoot, "src", "Ypopup.App", "Assets");
var pngPath = Path.Combine(assetsDir, "icon.png");
var trayIcoPath = Path.Combine(assetsDir, "tray.ico");
var appIcoPath = Path.Combine(assetsDir, "app.ico");

if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"Source icon not found: {sourcePath}");
    return 1;
}

Directory.CreateDirectory(assetsDir);
File.Copy(sourcePath, pngPath, overwrite: true);

using var source = new Bitmap(sourcePath);
IconWriter.SaveAsIco(trayIcoPath, [16, 24, 32, 48], source);
IconWriter.SaveAsIco(appIcoPath, [16, 24, 32, 48, 256], source);

Console.WriteLine($"Updated {pngPath}");
Console.WriteLine($"Created {trayIcoPath} (16,24,32,48)");
Console.WriteLine($"Created {appIcoPath} (16,24,32,48,256)");
return 0;

internal static class IconWriter
{
    public static void SaveAsIco(string path, int[] sizes, Bitmap source)
    {
        var entries = new List<byte[]>();

        foreach (var size in sizes)
        {
            using var bitmap = RenderSize(source, size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            entries.Add(stream.ToArray());
        }

        using var fileStream = File.Create(path);
        using var writer = new BinaryWriter(fileStream);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)entries.Count);

        var offset = 6 + 16 * entries.Count;
        for (var index = 0; index < entries.Count; index++)
        {
            var size = sizes[index];
            var data = entries[index];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }

        foreach (var data in entries)
        {
            writer.Write(data);
        }
    }

    private static Bitmap RenderSize(Bitmap source, int size)
    {
        var bounds = GetContentBounds(source);
        using var cropped = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var cropGraphics = Graphics.FromImage(cropped))
        {
            cropGraphics.DrawImage(
                source,
                new Rectangle(0, 0, bounds.Width, bounds.Height),
                bounds,
                GraphicsUnit.Pixel);
        }

        var fillRatio = size <= 16 ? 0.92f : 0.88f;
        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;

            if (size <= 24)
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.None;
            }
            else
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
            }

            var scale = Math.Min(size * fillRatio / cropped.Width, size * fillRatio / cropped.Height);
            var width = Math.Max(1, (int)Math.Round(cropped.Width * scale));
            var height = Math.Max(1, (int)Math.Round(cropped.Height * scale));
            var offsetX = (size - width) / 2;
            var offsetY = (size - height) / 2;

            graphics.DrawImage(cropped, new Rectangle(offsetX, offsetY, width, height));
        }

        return result;
    }

    private static Rectangle GetContentBounds(Bitmap bitmap)
    {
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = 0;
        var maxY = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 16)
                {
                    continue;
                }

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        if (maxX < minX)
        {
            return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
