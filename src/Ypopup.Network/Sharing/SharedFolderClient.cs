using System.Net.Http;
using System.Text.Json;
using Ypopup.Core.Models;
using Ypopup.Core.Network;

namespace Ypopup.Network.Sharing;

public static class SharedFolderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<SharedFolderListResponse> ListAsync(
        PeerInfo peer,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var encodedPath = Uri.EscapeDataString(relativePath.Replace('\\', '/'));
        var url = BuildUrl(peer, $"/api/list?path={encodedPath}");
        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<SharedFolderListResponse>(stream, JsonOptions, cancellationToken)
                   .ConfigureAwait(false)
                   ?? throw new InvalidDataException("공유폴더 목록을 해석할 수 없습니다.");
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException(
                $"공유폴더 연결 시간 초과 ({NetworkDefaults.ConnectTimeoutSeconds}초).\n" +
                $"주소: {url}\n" +
                "상대 PC에서 공유폴더가 켜져 있는지, TCP 포트(기본 50507) 방화벽 허용을 확인하세요.");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"공유폴더에 연결할 수 없습니다.\n주소: {url}\n{ex.Message}",
                ex);
        }
    }

    public static async Task DownloadAsync(
        PeerInfo peer,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var encodedPath = Uri.EscapeDataString(relativePath.Replace('\\', '/'));
        var url = BuildUrl(peer, $"/api/download?path={encodedPath}");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(NetworkDefaults.ConnectTimeoutSeconds)
        };
    }

    private static string BuildUrl(PeerInfo peer, string pathAndQuery)
    {
        return $"http://{peer.IpAddress}:{peer.ShareFolderPort}{pathAndQuery}";
    }
}
