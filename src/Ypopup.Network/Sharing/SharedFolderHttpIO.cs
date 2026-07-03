using System.Net.Sockets;
using System.Text;

namespace Ypopup.Network.Sharing;

internal static class SharedFolderHttpIO
{
    public static async Task<string> SendGetAsync(
        string host,
        int port,
        string pathAndQuery,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"공유폴더 연결 시간 초과 ({timeout.TotalSeconds:0}초).");
        }

        await using var stream = client.GetStream();
        var request = new StringBuilder()
            .Append("GET ").Append(pathAndQuery).Append(" HTTP/1.1\r\n")
            .Append("Host: ").Append(host).Append("\r\n")
            .Append("Connection: close\r\n")
            .Append("\r\n")
            .ToString();

        await stream.WriteAsync(Encoding.UTF8.GetBytes(request), timeoutCts.Token).ConfigureAwait(false);
        return await ReadResponseBodyAsync(stream, timeoutCts.Token).ConfigureAwait(false);
    }

    public static async Task<string?> ReadRequestLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerBytes = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
        if (headerBytes.Count == 0)
        {
            return null;
        }

        var headerText = Encoding.UTF8.GetString(headerBytes.ToArray());
        var lineEnd = headerText.IndexOf("\r\n", StringComparison.Ordinal);
        return lineEnd < 0 ? headerText.Trim() : headerText[..lineEnd];
    }

    private static async Task<List<byte>> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var received = new List<byte>(256);

        while (received.Count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            received.AddRange(buffer.AsSpan(0, read).ToArray());

            if (IndexOfHeaderTerminator(received) >= 0)
            {
                break;
            }
        }

        return received;
    }

    private static async Task<string> ReadResponseBodyAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerBytes = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
        if (headerBytes.Count == 0)
        {
            throw new InvalidDataException("공유폴더 서버 응답이 비어 있습니다.");
        }

        var headerText = Encoding.UTF8.GetString(headerBytes.ToArray());
        var headerEnd = IndexOfHeaderTerminator(headerBytes);
        if (headerEnd < 0)
        {
            throw new InvalidDataException("공유폴더 서버 응답 헤더를 읽을 수 없습니다.");
        }

        var statusLine = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                         ?? throw new InvalidDataException("공유폴더 서버 응답이 올바르지 않습니다.");

        if (!statusLine.Contains(" 200 ", StringComparison.Ordinal))
        {
            throw new HttpRequestException($"공유폴더 서버 오류: {statusLine}");
        }

        var contentLength = ParseContentLength(headerText);
        var bodyStart = headerEnd + 4;
        var bodyBytes = new List<byte>(contentLength > 0 ? contentLength : 0);

        if (bodyStart < headerBytes.Count)
        {
            bodyBytes.AddRange(headerBytes.Skip(bodyStart));
        }

        while (contentLength <= 0 || bodyBytes.Count < contentLength)
        {
            var buffer = new byte[8192];
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            bodyBytes.AddRange(buffer.AsSpan(0, read).ToArray());
        }

        if (contentLength > 0 && bodyBytes.Count < contentLength)
        {
            throw new EndOfStreamException("공유폴더 응답 본문이 중간에 끊겼습니다.");
        }

        var payload = contentLength > 0
            ? bodyBytes.Take(contentLength).ToArray()
            : bodyBytes.ToArray();

        return Encoding.UTF8.GetString(payload);
    }

    private static int IndexOfHeaderTerminator(IReadOnlyList<byte> data)
    {
        for (var index = 0; index + 3 < data.Count; index++)
        {
            if (data[index] == '\r' && data[index + 1] == '\n' && data[index + 2] == '\r' && data[index + 3] == '\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static int ParseContentLength(string headerText)
    {
        foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(line["Content-Length:".Length..].Trim(), out var length) && length >= 0)
            {
                return length;
            }
        }

        return -1;
    }
}
