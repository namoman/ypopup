using System.Security.Cryptography;
using Ypopup.Core.Models;

namespace Ypopup.Core.Settings;

/// <summary>
/// 프로필 사진은 설정 JSON이 아니라 AppData의 작은 PNG로 둡니다.
/// UDP 알림에는 해시만 실어, 사진이 바뀔 때만 상대가 TCP로 받습니다.
/// </summary>
public static class AvatarStore
{
    public const int MaxBytes = 200 * 1024;

    private static readonly object Gate = new();
    private static byte[]? _bytes;
    private static string _hash = string.Empty;
    private static bool _loaded;

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConstants.AppFolderName,
        "avatar.png");

    public static string CurrentHash()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return _hash;
        }
    }

    public static byte[]? ReadBytes()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return _bytes is null ? null : (byte[])_bytes.Clone();
        }
    }

    public static void Save(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (png.Length == 0 || png.Length > MaxBytes)
        {
            throw new InvalidDataException("프로필 사진 크기가 올바르지 않습니다.");
        }

        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, png);
            _bytes = (byte[])png.Clone();
            _hash = Hash(png);
            _loaded = true;
        }
    }

    public static void Delete()
    {
        lock (Gate)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            _bytes = null;
            _hash = string.Empty;
            _loaded = true;
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        if (!File.Exists(FilePath))
        {
            return;
        }

        var png = File.ReadAllBytes(FilePath);
        if (png.Length == 0 || png.Length > MaxBytes)
        {
            return;
        }

        _bytes = png;
        _hash = Hash(png);
    }

    private static string Hash(byte[] png)
        => Convert.ToHexString(SHA256.HashData(png))[..16];
}
