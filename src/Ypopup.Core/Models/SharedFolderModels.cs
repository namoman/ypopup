namespace Ypopup.Core.Models;

public sealed class SharedFolderEntry
{
    public required string Name { get; init; }
    public required string RelativePath { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; init; }

    public string DisplaySize => IsDirectory ? string.Empty : FormatSize(Size);

    public string ListLabel => IsDirectory ? $"📁 {Name}" : $"📄 {Name}  {DisplaySize}";

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}

public sealed class SharedFolderListResponse
{
    public string CurrentPath { get; set; } = string.Empty;
    public List<SharedFolderEntry> Entries { get; set; } = [];
}
