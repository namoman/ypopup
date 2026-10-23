using Ypopup.Core.Text;
using Xunit;

namespace Ypopup.Core.Tests.Text;

public class MessageLinkFinderTests
{
    [Fact]
    public void Find_WhenTextHasUrlAndUnc_ReturnsBoth()
    {
        var links = MessageLinkFinder.Find(@"자료 https://namoman.com/a 와 \\fileserver\share\doc.txt 참고");

        Assert.Equal(2, links.Count);
        Assert.Equal("https://namoman.com/a", links[0].Target);
        Assert.Equal(@"\\fileserver\share\doc.txt", links[1].Target);
    }

    [Fact]
    public void Find_WhenKoreanIsGluedToHost_StopsBeforeKorean()
    {
        const string text = "https://google.com좀 봐바";
        var link = Assert.Single(MessageLinkFinder.Find(text));
        Assert.Equal("https://google.com", link.Target);

        var parts = MessageLinkFinder.Split(text);
        Assert.Equal("좀 봐바", parts[1].Text);
        Assert.Null(parts[1].LinkTarget);
    }

    [Fact]
    public void Find_WhenUrlHasTrailingPunctuation_ExcludesIt()
    {
        var links = MessageLinkFinder.Find("여기 https://namoman.com/ypopup.");

        Assert.Equal("https://namoman.com/ypopup", Assert.Single(links).Target);
    }

    [Fact]
    public void Find_WhenWwwHasNoScheme_KeepsHost()
    {
        var link = Assert.Single(MessageLinkFinder.Find("www.namoman.com 봐"));

        Assert.Equal("https://www.namoman.com", MessageLinkFinder.ToLaunchTarget(link.Target));
    }

    [Fact]
    public void At_WhenIndexIsInsideLink_ReturnsThatLink()
    {
        const string text = "앞 https://a.com 뒤";
        var link = MessageLinkFinder.At(text, text.IndexOf("a.com", StringComparison.Ordinal));

        Assert.Equal("https://a.com", link?.Target);
    }

    [Fact]
    public void At_WhenIndexIsPlainText_ReturnsNull()
    {
        Assert.Null(MessageLinkFinder.At("앞 https://a.com 뒤", 0));
    }
}
