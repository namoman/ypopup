using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Ypopup.Core.Updates;

public enum UpdatePlatform
{
    Windows,
    MacArm64,
    MacX64,
}

public sealed record UpdateOffer(string LatestVersion, string DownloadUrl, string? FileName);

public sealed record UpdateCheckResult(UpdateOffer? Offer, string? Failure)
{
    public bool IsUpToDate => Failure is null && Offer is null;

    public static UpdateCheckResult UpToDate() => new(null, null);

    public static UpdateCheckResult Failed(string message) => new(null, message);
}

public static class GitHubUpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/namoman/ypopup/releases/latest";

    public static UpdatePlatform CurrentPlatform()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return UpdatePlatform.Windows;
        }

        return RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? UpdatePlatform.MacArm64
            : UpdatePlatform.MacX64;
    }

    public static async Task<UpdateCheckResult> CheckAsync(
        HttpClient http,
        string currentVersion,
        UpdatePlatform platform,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Y-popup", currentVersion));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Parse(currentVersion, json, platform);
    }

    public static UpdateCheckResult Parse(string currentVersion, string json, UpdatePlatform platform)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("tag_name", out var tagElement))
        {
            return UpdateCheckResult.Failed("릴리스 버전을 읽지 못했습니다.");
        }

        var latestText = NormalizeVersion(tagElement.GetString());
        var currentText = NormalizeVersion(currentVersion);
        if (!Version.TryParse(latestText, out var latest) || !Version.TryParse(currentText, out var current))
        {
            return UpdateCheckResult.Failed("릴리스 버전을 읽지 못했습니다.");
        }

        if (latest <= current)
        {
            return UpdateCheckResult.UpToDate();
        }

        var download = FindDownload(root, latestText, platform);
        if (download is null && TryTrustedUrl(root, "html_url", out var releasePage))
        {
            return new UpdateCheckResult(new UpdateOffer(latestText, releasePage, null), null);
        }

        if (download is null)
        {
            return UpdateCheckResult.Failed("받을 파일을 찾지 못했습니다.");
        }

        return new UpdateCheckResult(new UpdateOffer(latestText, download.Value.Url, download.Value.FileName), null);
    }

    private static (string FileName, string Url)? FindDownload(JsonElement root, string version, UpdatePlatform platform)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var preferred = PreferredFileNames(version, platform);
        foreach (var name in preferred)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var assetName = nameElement.GetString();
                if (!string.Equals(assetName, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryTrustedUrl(asset, "browser_download_url", out var url))
                {
                    return (assetName ?? name, url);
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> PreferredFileNames(string version, UpdatePlatform platform)
    {
        return platform switch
        {
            UpdatePlatform.MacArm64 =>
            [
                $"Y-popup-osx-arm64-{version}.zip",
                $"Y-popup-osx-arm64-{version}.dmg",
            ],
            UpdatePlatform.MacX64 =>
            [
                $"Y-popup-osx-x64-{version}.zip",
                $"Y-popup-osx-x64-{version}.dmg",
            ],
            _ => [$"Y-popup-{version}.exe"],
        };
    }

    private static bool TryTrustedUrl(JsonElement element, string propertyName, out string url)
    {
        url = "";
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        var candidate = value.GetString();
        if (string.IsNullOrWhiteSpace(candidate) || !IsTrustedReleaseUrl(candidate))
        {
            return false;
        }

        url = candidate;
        return true;
    }

    internal static bool IsTrustedReleaseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/namoman/ypopup/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string? version)
    {
        return (version ?? "").Trim().TrimStart('v', 'V');
    }
}
