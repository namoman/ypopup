using Ypopup.Core.Helpers;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Core.Sharing;
using Ypopup.Core.Settings;
using Ypopup.Network.Discovery;
using Ypopup.Network.Messaging;
using Ypopup.Network.Sharing;

namespace Ypopup.Network;

public sealed class YpopupCoordinator : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DiscoveryService _discoveryService;
    private readonly TcpHostService _tcpHostService;
    private readonly SharedFolderHostService _sharedFolderHostService;
    private readonly CancellationTokenSource _appCts = new();
    private readonly Dictionary<string, DateTime> _lastAutoReplyTimes = new(StringComparer.OrdinalIgnoreCase);
    private int _disposed;

    public SharedFolderHostStartResult SharedFolderHostStatus { get; private set; } = new(false);

    public event Action<IReadOnlyList<PeerInfo>>? PeersChanged;
    public event Action<ReceivedMessage>? MessageReceived;
    public event Action? SettingsSaved;

    public YpopupCoordinator()
    {
        _settingsService = new SettingsService();
        _discoveryService = new DiscoveryService(
            _settingsService,
            () => IsAway,
            () => _sharedFolderHostService.IsRunning);
        _tcpHostService = new TcpHostService(_settingsService);
        _sharedFolderHostService = new SharedFolderHostService(_settingsService);

        _discoveryService.PeersChanged += peers => PeersChanged?.Invoke(peers);
        _tcpHostService.MessageReceived += HandleMessageReceivedAsync;
    }

    public bool IsAway { get; set; }

    public AppSettings Settings => _settingsService.Current;

    public IReadOnlyList<PeerInfo> GetPeers() => _discoveryService.GetPeers();

    public DateTime LastAnnounceSentUtc => _discoveryService.LastAnnounceSentUtc;
    public DateTime LastPacketReceivedUtc => _discoveryService.LastPacketReceivedUtc;

    public async Task StartAsync()
    {
        SharedFolderHostStatus = await _sharedFolderHostService.StartAsync(_appCts.Token).ConfigureAwait(false);
        await _discoveryService.StartAsync(_appCts.Token).ConfigureAwait(false);
        await _tcpHostService.StartAsync(_appCts.Token).ConfigureAwait(false);
    }

    public void SaveSettings(AppSettings settings)
    {
        var restartShareFolder = settings.ShareFolderEnabled != Settings.ShareFolderEnabled
                                 || !string.Equals(settings.ShareFolderPath, Settings.ShareFolderPath, StringComparison.OrdinalIgnoreCase)
                                 || settings.ShareFolderPort != Settings.ShareFolderPort;

        var restartTcpHost = settings.TcpPort != Settings.TcpPort;
        var restartDiscovery = settings.DiscoveryPort != Settings.DiscoveryPort
                                || settings.PreferredLocalIp != Settings.PreferredLocalIp;

        _settingsService.Save(settings);
        SettingsSaved?.Invoke();

        if (restartShareFolder || (settings.ShareFolderEnabled && !SharedFolderHostStatus.IsRunning))
        {
            _ = BackgroundTaskTracker.RunAsync("공유폴더 재시작", () => RestartSharedFolderAsync());
        }

        if (restartTcpHost)
        {
            _ = BackgroundTaskTracker.RunAsync("TCP 호스트 재시작", () => _tcpHostService.RestartAsync(_appCts.Token));
        }

        if (restartDiscovery)
        {
            _ = BackgroundTaskTracker.RunAsync("Discovery 재시작", () => RestartDiscoveryAsync());
        }
    }

    public Task<SharedFolderListResponse> ListSharedFolderAsync(
        PeerInfo peer,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        return SharedFolderClient.ListAsync(peer, relativePath, cancellationToken);
    }

    public Task DownloadSharedFileAsync(
        PeerInfo peer,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
        => DownloadSharedFileAsync(peer, relativePath, destinationPath, cancellationToken, progress: null);

    public Task DownloadSharedFileAsync(
        PeerInfo peer,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress)
        => SharedFolderClient.DownloadAsync(peer, relativePath, destinationPath, cancellationToken, progress);

    public Task SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
        => SendMessageAsync(message, cancellationToken, progress: null);

    public Task SendMessageAsync(
        OutgoingMessage message,
        CancellationToken cancellationToken,
        IProgress<TransferProgress>? progress)
        => TcpHostService.SendMessageAsync(message, _settingsService.Current, cancellationToken, progress);

    private async Task RestartSharedFolderAsync()
    {
        try
        {
            SharedFolderHostStatus = await _sharedFolderHostService.StartAsync(_appCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SharedFolderHostStatus = new SharedFolderHostStartResult(false, ex.Message);
            LogService.Error("Coordinator", $"Shared folder restart: {ex.Message}");
        }
    }

    private async Task RestartDiscoveryAsync()
    {
        try
        {
            await _discoveryService.RestartAsync(_appCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogService.Error("Coordinator", $"Discovery restart: {ex.Message}");
        }
    }

    private async void HandleMessageReceivedAsync(ReceivedMessage message)
    {
        if (IsAway && !message.IsAutoReply && !string.IsNullOrWhiteSpace(Settings.AwayMessage))
        {
            var shouldReply = false;
            lock (_lastAutoReplyTimes)
            {
                var now = DateTime.UtcNow;
                if (!_lastAutoReplyTimes.TryGetValue(message.SenderId, out var lastTime)
                    || now - lastTime > TimeSpan.FromMinutes(1))
                {
                    _lastAutoReplyTimes[message.SenderId] = now;
                    shouldReply = true;
                }
            }

            if (shouldReply)
            {
                var peer = _discoveryService.FindPeer(message.SenderId)
                           ?? new PeerInfo
                           {
                               MachineId = message.SenderId,
                               DisplayName = message.SenderName,
                               IpAddress = message.SenderIpAddress,
                               TcpPort = Settings.TcpPort
                           };

                _ = BackgroundTaskTracker.RunAsync("자동답장", () => SendMessageAsync(new OutgoingMessage
                {
                    Recipient = peer,
                    Body = Settings.AwayMessage,
                    IsAutoReply = true
                }));
            }
        }

        MessageReceived?.Invoke(message);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _appCts.CancelAsync().ConfigureAwait(false);
        await _discoveryService.DisposeAsync().ConfigureAwait(false);
        await _tcpHostService.DisposeAsync().ConfigureAwait(false);
        await _sharedFolderHostService.DisposeAsync().ConfigureAwait(false);
        _appCts.Dispose();
    }
}
