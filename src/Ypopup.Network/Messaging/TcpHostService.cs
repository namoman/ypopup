using System.Net;
using System.Net.Sockets;
using Ypopup.Core.IO;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Protocol;
using Ypopup.Core.Settings;

namespace Ypopup.Network.Messaging;

public sealed class TcpHostService : IAsyncDisposable
{
    private const int MaxConcurrentConnections = 20;

    private readonly SettingsService _settingsService;
    private readonly ConnectionLimiter _connectionLimiter = new(MaxConcurrentConnections);
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private int _disposed;

    public event Action<ReceivedMessage>? MessageReceived;

    public TcpHostService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await StopAsync().ConfigureAwait(false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Any, _settingsService.Current.TcpPort);
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_listener is null)
        {
            return Task.CompletedTask;
        }

        if (_cts is not null)
        {
            _cts.Cancel();
        }

        _listener.Stop();

        if (_acceptTask is not null)
        {
            try
            {
                _acceptTask.Wait();
            }
            catch (OperationCanceledException)
            {
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
            }
        }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _acceptTask = null;
        return Task.CompletedTask;
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Ypopup.Core.Helpers.BackgroundTaskTracker.RunAsync("TCP 클라이언트 처리", async () =>
                {
                    using var connection = await _connectionLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await HandleClientAsync(client, cancellationToken).ConfigureAwait(false);
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                LogService.Warning("TcpHost", $"Accept error: {ex.Message}");
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var pendingFiles = new List<(string TempPath, string FinalPath)>();

        try
        {
            await using var stream = client.GetStream();
            var packet = await PacketCodec.ReadPacketAsync(stream, cancellationToken).ConfigureAwait(false);
            if (packet is null || packet.Type != PacketType.TextMessage)
            {
                return;
            }

            foreach (var attachment in packet.Attachments)
            {
                var safeName = FileNameSanitizer.Sanitize(attachment.FileName);
                var finalPath = FileNameSanitizer.GetUniquePath(Path.Combine(_settingsService.Current.ReceiveDirectory, safeName));
                var tempPath = finalPath + ".partial";

                await PacketCodec.SaveFileAsync(stream, tempPath, attachment.Size, cancellationToken)
                    .ConfigureAwait(false);
                pendingFiles.Add((tempPath, finalPath));
            }

            var savedPaths = new List<string>();
            foreach (var (tempPath, finalPath) in pendingFiles)
            {
                File.Move(tempPath, finalPath, overwrite: false);
                savedPaths.Add(finalPath);
            }

            pendingFiles.Clear();

            MessageReceived?.Invoke(new ReceivedMessage
            {
                MessageId = packet.MessageId,
                SenderId = packet.SenderId,
                SenderName = packet.SenderName,
                SenderIpAddress = ResolveClientIpAddress(client),
                Body = packet.Body,
                SavedFilePaths = savedPaths,
                IsAutoReply = packet.IsAutoReply
            });
        }
        catch (Exception ex)
        {
            LogService.Error("TcpHost", $"Client handling: {ex.Message}");
            CleanupPartialFiles(pendingFiles);
        }
        finally
        {
            client.Dispose();
        }
    }

    public static Task SendMessageAsync(OutgoingMessage message, AppSettings settings, CancellationToken cancellationToken)
        => SendMessageAsync(message, settings, cancellationToken, progress: null);

    public static async Task SendMessageAsync(
        OutgoingMessage message,
        AppSettings settings,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(NetworkDefaults.ConnectTimeoutSeconds));

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(
                    NetworkAddressHelper.ParseConnectableAddress(message.Recipient.IpAddress),
                    message.Recipient.TcpPort,
                    timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"연결 시간이 초과되었습니다 ({NetworkDefaults.ConnectTimeoutSeconds}초). 상대 PC가 켜져 있는지 확인하세요.");
        }

        await using var stream = client.GetStream();

        var attachments = new List<FileAttachmentInfo>();
        foreach (var path in message.AttachmentPaths)
        {
            var fileInfo = new FileInfo(path);
            attachments.Add(new FileAttachmentInfo
            {
                FileName = fileInfo.Name,
                Size = fileInfo.Length
            });

            progress?.Report(new TransferProgress(0, fileInfo.Length, true, fileInfo.Name));
        }

        var packet = new LanPacket
        {
            Type = PacketType.TextMessage,
            SenderId = settings.MachineId,
            SenderName = settings.DisplayName,
            Body = message.Body,
            Attachments = attachments,
            IsAutoReply = message.IsAutoReply
        };

        await PacketCodec.WritePacketAsync(stream, packet, cancellationToken).ConfigureAwait(false);

        foreach (var path in message.AttachmentPaths)
        {
            await PacketCodec.WriteFileAsync(stream, path, cancellationToken, progress).ConfigureAwait(false);
        }
    }

    private static string ResolveClientIpAddress(TcpClient client)
    {
        if (client.Client.RemoteEndPoint is IPEndPoint endpoint)
        {
            return NetworkAddressHelper.NormalizeToConnectableAddress(endpoint.Address);
        }

        return "127.0.0.1";
    }

    private static void CleanupPartialFiles(IEnumerable<(string TempPath, string FinalPath)> pendingFiles)
    {
        foreach (var (tempPath, _) in pendingFiles)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                LogService.Warning("TcpHost", $"Partial file cleanup: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        StopAsync();
        await _connectionLimiter.DisposeAsync().ConfigureAwait(false);
    }
}
