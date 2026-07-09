using Xunit;
using Ypopup.Core.IO;

namespace Ypopup.Core.Tests.IO;

public class FileNameSanitizerTests
{
    [Fact]
    public void Sanitize_PlainName_ReturnsAsIs()
    {
        var result = FileNameSanitizer.Sanitize("document.txt");

        Assert.Equal("document.txt", result);
    }

    [Fact]
    public void Sanitize_InvalidChars_ReplacedWithUnderscore()
    {
        var name = "a" + Path.GetInvalidFileNameChars().First() + "b";

        var result = FileNameSanitizer.Sanitize(name);

        Assert.Equal("a_b", result);
    }

    [Fact]
    public void Sanitize_Empty_FallsBackToReceivedBin()
    {
        Assert.Equal("received.bin", FileNameSanitizer.Sanitize(""));
        Assert.Equal("received.bin", FileNameSanitizer.Sanitize("   "));
    }

    [Fact]
    public void GetUniquePath_NonExisting_ReturnsAsIs()
    {
        using var temp = new TempDir();
        var candidate = Path.Combine(temp.Path, "new.txt");

        Assert.Equal(candidate, FileNameSanitizer.GetUniquePath(candidate));
    }

    [Fact]
    public void GetUniquePath_Existing_AppendsIndex()
    {
        using var temp = new TempDir();
        var first = Path.Combine(temp.Path, "dup.txt");
        File.WriteAllText(first, "x");

        var second = FileNameSanitizer.GetUniquePath(first);

        Assert.Equal(Path.Combine(temp.Path, "dup (1).txt"), second);
    }

    [Fact]
    public void GetUniquePath_MultipleExisting_AppendsIncrementalIndex()
    {
        using var temp = new TempDir();
        var first = Path.Combine(temp.Path, "dup.txt");
        File.WriteAllText(first, "x");
        File.WriteAllText(Path.Combine(temp.Path, "dup (1).txt"), "x");

        var result = FileNameSanitizer.GetUniquePath(first);

        Assert.Equal(Path.Combine(temp.Path, "dup (2).txt"), result);
    }
}