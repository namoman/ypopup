using System.Text.Json;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Protocol;

namespace Ypopup.Network.Sharing;

public static class SharedFolderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(NetworkDefaults.ConnectTimeoutSeconds);

    public static async Task<SharedFolderListResponse> ListAsync(
        PeerInfo peer,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var encodedPath = Uri.EscapeDataString(relativePath.Replace('\\', '/'));
        var pathAndQuery = $"/api/list?path={encodedPath}";

        try
        {
            var body = await SharedFolderHttpIO.SendGetAsync(
                peer.IpAddress,
                peer.ShareFolderPort,
                pathAndQuery,
                RequestTimeout,
                cancellationToken).ConfigureAwait(false);

            return JsonSerializer.Deserialize<SharedFolderListResponse>(body, JsonOptions)
                   ?? throw new InvalidDataException("공유폴더 목록을 해석할 수 없습니다.");
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"{ex.Message}\n" +
                $"주소: http://{peer.IpAddress}:{peer.ShareFolderPort}{pathAndQuery}\n" +
                "상대 PC에서 공유폴더가 켜져 있는지, TCP 포트(기본 50507) 방화벽 허용을 확인하세요.",
                ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or EndOfStreamException)
        {
            throw new HttpRequestException(
                $"공유폴더에 연결할 수 없습니다.\n주소: http://{peer.IpAddress}:{peer.ShareFolderPort}{pathAndQuery}\n{ex.Message}",
                ex);
        }
    }

    public static Task DownloadAsync(
        PeerInfo peer,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
        => DownloadAsync(peer, relativePath, destinationPath, cancellationToken, progress: null);

    public static async Task DownloadAsync(
        PeerInfo peer,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress)
    {
        var encodedPath = Uri.EscapeDataString(relativePath.Replace('\\', '/'));
        var pathAndQuery = $"/api/download?path={encodedPath}";
        await DownloadBinaryAsync(peer, pathAndQuery, destinationPath, cancellationToken, progress).ConfigureAwait(false);
    }

    private static async Task DownloadBinaryAsync(
        PeerInfo peer,
        string pathAndQuery,
        string destinationPath,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tempPath = destinationPath + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using var client = new System.Net.Sockets.TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            await client.ConnectAsync(peer.IpAddress, peer.ShareFolderPort, timeoutCts.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();

            var request = $"GET {pathAndQuery} HTTP/1.1\r\nHost: {peer.IpAddress}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(request), timeoutCts.Token).ConfigureAwait(false);

            var headerBytes = new MemoryStream();
            var buffer = new byte[8192];
            var headerEndFound = false;
            var headerEndIndex = -1;

            while (!headerEndFound)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    throw new EndOfStreamException("공유폴더 다운로드 응답이 비어 있습니다.");
                }

                headerBytes.Write(buffer, 0, read);
                var data = headerBytes.ToArray();
                for (var index = 0; index + 3 < data.Length; index++)
                {
                    if (data[index] == '\r' && data[index + 1] == '\n' && data[index + 2] == '\r' && data[index + 3] == '\n')
                    {
                        headerEndFound = true;
                        headerEndIndex = index + 4;
                        break;
                    }
                }
            }

            var headerText = System.Text.Encoding.UTF8.GetString(headerBytes.ToArray(), 0, headerEndIndex);
            if (!headerText.StartsWith("HTTP/1.1 200", StringComparison.Ordinal))
            {
                throw new HttpRequestException($"공유폴더 다운로드 실패: {headerText.Split('\r', '\n')[0]}");
            }

            var totalBytes = ParseContentLength(headerText);
            if (totalBytes <= 0)
            {
                throw new InvalidDataException("공유폴더 응답에 Content-Length가 없습니다.");
            }

            var fileName = Path.GetFileName(destinationPath);
            var reporter = new ProgressThresholdReporter(totalBytes);
            reporter.ReportIfReady(progress, 0, isSending: false, fileName);

            await using var destination = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var allBytes = headerBytes.ToArray();
            var headerRemainder = allBytes.Length - headerEndIndex;
            long transferred = 0;

            if (headerRemainder > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await destination.WriteAsync(allBytes.AsMemory(headerEndIndex, headerRemainder), cancellationToken)
                    .ConfigureAwait(false);
                transferred += headerRemainder;
                reporter.ReportIfReady(progress, transferred, isSending: false, fileName);
            }

            while (transferred < totalBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                transferred += read;
                reporter.ReportIfReady(progress, transferred, isSending: false, fileName);
            }

            if (transferred < totalBytes)
            {
                throw new EndOfStreamException("공유폴더 다운로드가 중간에 끊겼습니다.");
            }

            reporter.ReportIfReady(progress, transferred, isSending: false, fileName);
        }
        catch
        {
            TryDeletePartial(tempPath);
            throw;
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    private static long ParseContentLength(string headerText)
    {
        foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line["Content-Length:".Length..].Trim();
            return long.TryParse(value, out var length) && length >= 0 ? length : -1;
        }

        return -1;
    }

    private static void TryDeletePartial(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
        }
    }
}