using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Ypopup.Core.Models;
using Ypopup.Core.Settings;
using Ypopup.Core.Sharing;

namespace Ypopup.Network.Sharing;

public sealed class SharedFolderHostService : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SettingsService _settingsService;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private int _disposed;

    public SharedFolderHostService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsRunning => _listener is not null;

    public IReadOnlyList<string> BoundPrefixes { get; private set; } = [];

    public async Task<SharedFolderHostStartResult> StartAsync(CancellationToken cancellationToken)
    {
        await StopAsync().ConfigureAwait(false);

        var settings = _settingsService.Current;
        if (!settings.ShareFolderEnabled)
        {
            BoundPrefixes = [];
            return new SharedFolderHostStartResult(false);
        }

        Directory.CreateDirectory(settings.ShareFolderPath);

        try
        {
            _listener = new TcpListener(IPAddress.Any, settings.ShareFolderPort);
            _listener.Start();
            BoundPrefixes = [$"*:{settings.ShareFolderPort}"];
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _acceptTask = AcceptLoopAsync(_cts.Token);
            return new SharedFolderHostStartResult(true, BoundPrefixes: BoundPrefixes);
        }
        catch (Exception ex)
        {
            BoundPrefixes = [];
            return new SharedFolderHostStartResult(false, $"공유폴더 서버 시작 실패: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        if (_listener is null)
        {
            BoundPrefixes = [];
            return;
        }

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        _listener.Stop();

        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _acceptTask = null;
        BoundPrefixes = [];
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Share folder accept error: {ex.Message}");
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 30000;

            try
            {
                await using var stream = client.GetStream();
                var requestLine = await SharedFolderHttpIO.ReadRequestLineAsync(stream, cancellationToken).ConfigureAwait(false);
                if (requestLine is null)
                {
                    return;
                }

                var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteTextResponseAsync(stream, 405, "Method Not Allowed", "Method Not Allowed", cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (!Uri.TryCreate("http://localhost" + parts[1], UriKind.Absolute, out var uri))
                {
                    await WriteTextResponseAsync(stream, 400, "Bad Request", "Bad Request", cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                var relativePath = GetQueryParameter(uri.Query, "path") ?? string.Empty;
                var path = uri.AbsolutePath;

                if (path.Equals("/api/list", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleListAsync(stream, relativePath, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (path.Equals("/api/download", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleDownloadAsync(stream, relativePath, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteTextResponseAsync(stream, 404, "Not Found", "Not Found", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Share folder client error: {ex.Message}");
            }
        }
    }

    private static string? GetQueryParameter(string query, string key)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(part[..separator]);
            if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return null;
    }

    private async Task HandleListAsync(NetworkStream stream, string relativePath, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;

        string directoryPath;
        try
        {
            directoryPath = SharedFolderPathHelper.ResolveSafeFullPath(settings.ShareFolderPath, relativePath);
        }
        catch (InvalidOperationException)
        {
            await WriteTextResponseAsync(stream, 400, "Bad Request", "Bad Request", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!Directory.Exists(directoryPath))
        {
            await WriteTextResponseAsync(stream, 404, "Not Found", "Not Found", cancellationToken).ConfigureAwait(false);
            return;
        }

        var entries = new List<SharedFolderEntry>();
        foreach (var dir in Directory.GetDirectories(directoryPath))
        {
            var info = new DirectoryInfo(dir);
            entries.Add(new SharedFolderEntry
            {
                Name = info.Name,
                RelativePath = SharedFolderPathHelper.ToRelativePath(settings.ShareFolderPath, info.FullName),
                IsDirectory = true,
                Size = 0
            });
        }

        foreach (var file in Directory.GetFiles(directoryPath))
        {
            var info = new FileInfo(file);
            entries.Add(new SharedFolderEntry
            {
                Name = info.Name,
                RelativePath = SharedFolderPathHelper.ToRelativePath(settings.ShareFolderPath, info.FullName),
                IsDirectory = false,
                Size = info.Length
            });
        }

        var payload = new SharedFolderListResponse
        {
            CurrentPath = relativePath.Replace('\\', '/').Trim('/'),
            Entries = entries.OrderBy(entry => !entry.IsDirectory).ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await WriteTextResponseAsync(stream, 200, "OK", json, cancellationToken, "application/json; charset=utf-8")
            .ConfigureAwait(false);
    }

    private async Task HandleDownloadAsync(NetworkStream stream, string relativePath, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            await WriteTextResponseAsync(stream, 400, "Bad Request", "Bad Request", cancellationToken).ConfigureAwait(false);
            return;
        }

        string filePath;
        try
        {
            filePath = SharedFolderPathHelper.ResolveSafeFullPath(settings.ShareFolderPath, relativePath);
        }
        catch (InvalidOperationException)
        {
            await WriteTextResponseAsync(stream, 400, "Bad Request", "Bad Request", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!File.Exists(filePath))
        {
            await WriteTextResponseAsync(stream, 404, "Not Found", "Not Found", cancellationToken).ConfigureAwait(false);
            return;
        }

        var fileName = Path.GetFileName(filePath);
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var header = BuildHttpHeader(
            200,
            "OK",
            "application/octet-stream",
            fileStream.Length,
            $"attachment; filename=\"{fileName}\"");
        await stream.WriteAsync(Encoding.UTF8.GetBytes(header), cancellationToken).ConfigureAwait(false);
        await fileStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteTextResponseAsync(
        NetworkStream stream,
        int statusCode,
        string statusText,
        string body,
        CancellationToken cancellationToken,
        string contentType = "text/plain; charset=utf-8")
    {
        var header = BuildHttpHeader(statusCode, statusText, contentType, Encoding.UTF8.GetByteCount(body));
        await stream.WriteAsync(Encoding.UTF8.GetBytes(header), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(body), cancellationToken).ConfigureAwait(false);
    }

    private static string BuildHttpHeader(
        int statusCode,
        string statusText,
        string contentType,
        long contentLength,
        string? contentDisposition = null)
    {
        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(statusText).Append("\r\n");
        sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
        sb.Append("Content-Length: ").Append(contentLength).Append("\r\n");
        sb.Append("Connection: close\r\n");
        if (!string.IsNullOrEmpty(contentDisposition))
        {
            sb.Append("Content-Disposition: ").Append(contentDisposition).Append("\r\n");
        }

        sb.Append("\r\n");
        return sb.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
    }
}
