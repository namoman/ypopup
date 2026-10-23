using Ypopup.Core.Updates;
using Xunit;

namespace Ypopup.Core.Tests.Updates;

public class GitHubUpdateCheckerTests
{
    [Fact]
    public void Parse_WhenReleaseIsNewer_ReturnsWindowsExe()
    {
        var json = ReleaseJson(
            "v2.3.0",
            "https://github.com/namoman/ypopup/releases/tag/v2.3.0",
            ("Y-popup-2.3.0.exe", "https://github.com/namoman/ypopup/releases/download/v2.3.0/Y-popup-2.3.0.exe"),
            ("Y-popup-net8-2.3.0.exe", "https://github.com/namoman/ypopup/releases/download/v2.3.0/Y-popup-net8-2.3.0.exe"));

        var result = GitHubUpdateChecker.Parse("2.2.4", json, UpdatePlatform.Windows);

        Assert.Null(result.Failure);
        Assert.NotNull(result.Offer);
        Assert.Equal("2.3.0", result.Offer.LatestVersion);
        Assert.Equal("Y-popup-2.3.0.exe", result.Offer.FileName);
        Assert.EndsWith("/Y-popup-2.3.0.exe", result.Offer.DownloadUrl);
    }

    [Fact]
    public void Parse_WhenReleaseMatchesCurrent_IsUpToDate()
    {
        var json = ReleaseJson(
            "v2.2.4",
            "https://github.com/namoman/ypopup/releases/tag/v2.2.4",
            ("Y-popup-2.2.4.exe", "https://github.com/namoman/ypopup/releases/download/v2.2.4/Y-popup-2.2.4.exe"));

        var result = GitHubUpdateChecker.Parse("2.2.4", json, UpdatePlatform.Windows);

        Assert.True(result.IsUpToDate);
    }

    [Fact]
    public void Parse_WhenAssetMissing_FallsBackToReleasePage()
    {
        var json = ReleaseJson(
            "v2.3.0",
            "https://github.com/namoman/ypopup/releases/tag/v2.3.0");

        var result = GitHubUpdateChecker.Parse("2.2.4", json, UpdatePlatform.Windows);

        Assert.NotNull(result.Offer);
        Assert.Null(result.Offer.FileName);
        Assert.Equal("https://github.com/namoman/ypopup/releases/tag/v2.3.0", result.Offer.DownloadUrl);
    }

    [Fact]
    public void Parse_WhenMacArm_PrefersZip()
    {
        var json = ReleaseJson(
            "v2.3.0",
            "https://github.com/namoman/ypopup/releases/tag/v2.3.0",
            ("Y-popup-osx-arm64-2.3.0.dmg", "https://github.com/namoman/ypopup/releases/download/v2.3.0/Y-popup-osx-arm64-2.3.0.dmg"),
            ("Y-popup-osx-arm64-2.3.0.zip", "https://github.com/namoman/ypopup/releases/download/v2.3.0/Y-popup-osx-arm64-2.3.0.zip"));

        var result = GitHubUpdateChecker.Parse("2.2.4", json, UpdatePlatform.MacArm64);

        Assert.Equal("Y-popup-osx-arm64-2.3.0.zip", result.Offer?.FileName);
    }

    [Fact]
    public void Parse_WhenDownloadUrlIsNotThisRepo_IgnoresAsset()
    {
        var json = ReleaseJson(
            "v2.3.0",
            "https://github.com/namoman/ypopup/releases/tag/v2.3.0",
            ("Y-popup-2.3.0.exe", "https://example.com/Y-popup-2.3.0.exe"));

        var result = GitHubUpdateChecker.Parse("2.2.4", json, UpdatePlatform.Windows);

        Assert.Null(result.Offer?.FileName);
        Assert.Equal("https://github.com/namoman/ypopup/releases/tag/v2.3.0", result.Offer?.DownloadUrl);
    }

    private static string ReleaseJson(string tag, string htmlUrl, params (string Name, string Url)[] assets)
    {
        var assetJson = string.Join(
            ",",
            assets.Select(asset => $$"""{"name":"{{asset.Name}}","browser_download_url":"{{asset.Url}}"}"""));
        return $$"""{"tag_name":"{{tag}}","html_url":"{{htmlUrl}}","assets":[{{assetJson}}]}""";
    }
}
