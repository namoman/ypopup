using Xunit;
using Ypopup.Core.Sharing;

namespace Ypopup.Core.Tests.Sharing;

public class SharedFolderPathHelperTests
{
    [Fact]
    public void ResolveSafeFullPath_EmptyRelative_ReturnsRoot()
    {
        using var temp = new TempRoot();
        var root = temp.Path;

        var result = SharedFolderPathHelper.ResolveSafeFullPath(root, string.Empty);

        Assert.Equal(NormalizeDir(root), NormalizeDir(result));
    }

    [Fact]
    public void ResolveSafeFullPath_NormalizesBackslashAndSlash()
    {
        using var temp = new TempRoot();
        var root = temp.Path;
        Directory.CreateDirectory(Path.Combine(root, "sub"));

        var result = SharedFolderPathHelper.ResolveSafeFullPath(root, "sub\\inside/..");

        Assert.StartsWith(NormalizeDir(root), NormalizeDir(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveSafeFullPath_DirectoryTraversal_Throws()
    {
        using var temp = new TempRoot();
        var root = temp.Path;

        Assert.Throws<InvalidOperationException>(
            () => SharedFolderPathHelper.ResolveSafeFullPath(root, "../../../etc"));
    }

    [Fact]
    public void ToRelativePath_OutsideRoot_Throws()
    {
        using var temp = new TempRoot();
        using var other = new TempRoot();
        Directory.CreateDirectory(Path.Combine(temp.Path, "a"));

        Assert.Throws<InvalidOperationException>(
            () => SharedFolderPathHelper.ToRelativePath(temp.Path, other.Path));
    }

    [Fact]
    public void ToRelativePath_InsideRoot_ReturnsRelative()
    {
        using var temp = new TempRoot();
        var dir = Path.Combine(temp.Path, "sub");
        Directory.CreateDirectory(dir);

        var relative = SharedFolderPathHelper.ToRelativePath(temp.Path, dir);

        Assert.Equal("sub", relative);
    }

    private static string NormalizeDir(string p) =>
        Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private sealed class TempRoot : IDisposable
    {
        public string Path { get; }

        public TempRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ypopup-path-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}