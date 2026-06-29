using System.Net;
using System.Net.Sockets;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Protocol;
using Ypopup.Core.Settings;

namespace Ypopup.Network.Messaging;

public sealed class TcpHostService : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
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
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Any, _settingsService.Current.TcpPort);
        _listener.Start();
        _acceptTask = AcceptLoopAsync(_cts.Token);
        await Task.CompletedTask;
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
                System.Diagnostics.Debug.WriteLine($"TCP accept error: {ex.Message}");
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
                var safeName = SanitizeFileName(attachment.FileName);
                var finalPath = GetUniquePath(Path.Combine(_settingsService.Current.ReceiveDirectory, safeName));
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
            System.Diagnostics.Debug.WriteLine($"TCP client handling error: {ex.Message}");
            CleanupPartialFiles(pendingFiles);
        }
        finally
        {
            client.Dispose();
        }
    }

    public static async Task SendMessageAsync(OutgoingMessage message, AppSettings settings, CancellationToken cancellationToken)
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
            await PacketCodec.WriteFileAsync(stream, path, cancellationToken).ConfigureAwait(false);
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
                System.Diagnostics.Debug.WriteLine($"Partial file cleanup error: {ex.Message}");
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "received.bin" : fileName;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;

        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            index++;
        } while (File.Exists(candidate));

        return candidate;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        _listener?.Stop();

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

        _cts?.Dispose();
    }
}
