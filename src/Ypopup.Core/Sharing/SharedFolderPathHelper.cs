namespace Ypopup.Core.Sharing;

public static class SharedFolderPathHelper
{
    public static string GetDefaultShareFolderPath()
    {
        var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        return Path.Combine(exeDirectory, Models.AppConstants.DefaultShareFolderName);
    }

    public static string ResolveSafeFullPath(string rootDirectory, string relativePath)
    {
        var root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(root);

        var normalized = relativePath.Replace('\\', '/').Trim('/');
        var candidate = string.IsNullOrEmpty(normalized)
            ? root
            : Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));

        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("허용되지 않은 경로입니다.");
        }

        return candidate;
    }

    public static string ToRelativePath(string rootDirectory, string fullPath)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(fullPath);

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("허용되지 않은 경로입니다.");
        }

        return full[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
    }
}
