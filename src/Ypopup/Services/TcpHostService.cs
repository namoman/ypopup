using System.Net;
using System.Net.Sockets;
using Ypopup.Models;
using Ypopup.Protocol;

namespace Ypopup.Services;

public sealed class TcpHostService : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;

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
        try
        {
            await using var stream = client.GetStream();
            var packet = await PacketCodec.ReadPacketAsync(stream, cancellationToken).ConfigureAwait(false);
            if (packet is null || packet.Type != PacketType.TextMessage)
            {
                return;
            }

            var savedPaths = new List<string>();
            foreach (var attachment in packet.Attachments)
            {
                var safeName = SanitizeFileName(attachment.FileName);
                var destination = GetUniquePath(Path.Combine(_settingsService.Current.ReceiveDirectory, safeName));
                await PacketCodec.SaveFileAsync(stream, destination, attachment.Size, cancellationToken)
                    .ConfigureAwait(false);
                savedPaths.Add(destination);
            }

            MessageReceived?.Invoke(new ReceivedMessage
            {
                MessageId = packet.MessageId,
                SenderId = packet.SenderId,
                SenderName = packet.SenderName,
                Body = packet.Body,
                SavedFilePaths = savedPaths
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TCP client handling error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }

    public static async Task SendMessageAsync(OutgoingMessage message, AppSettings settings, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(message.Recipient.IpAddress), message.Recipient.TcpPort, cancellationToken)
            .ConfigureAwait(false);

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
            Attachments = attachments
        };

        await PacketCodec.WritePacketAsync(stream, packet, cancellationToken).ConfigureAwait(false);

        foreach (var path in message.AttachmentPaths)
        {
            await PacketCodec.WriteFileAsync(stream, path, cancellationToken).ConfigureAwait(false);
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
