using Ypopup.Core.Models;
using Ypopup.Core.Settings;
using Ypopup.Network.Discovery;
using Ypopup.Network.Messaging;

namespace Ypopup.Network;

public sealed class YpopupCoordinator : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DiscoveryService _discoveryService;
    private readonly TcpHostService _tcpHostService;
    private readonly CancellationTokenSource _appCts = new();
    private int _disposed;

    public event Action<IReadOnlyList<PeerInfo>>? PeersChanged;
    public event Action<ReceivedMessage>? MessageReceived;

    public YpopupCoordinator()
    {
        _settingsService = new SettingsService();
        _discoveryService = new DiscoveryService(_settingsService, () => IsAway);
        _tcpHostService = new TcpHostService(_settingsService);

        _discoveryService.PeersChanged += peers => PeersChanged?.Invoke(peers);
        _tcpHostService.MessageReceived += HandleMessageReceivedAsync;
    }

    public bool IsAway { get; set; }

    public AppSettings Settings => _settingsService.Current;

    public IReadOnlyList<PeerInfo> GetPeers() => _discoveryService.GetPeers();

    public async Task StartAsync()
    {
        await _discoveryService.StartAsync(_appCts.Token).ConfigureAwait(false);
        await _tcpHostService.StartAsync(_appCts.Token).ConfigureAwait(false);
    }

    public void SaveSettings(AppSettings settings)
    {
        _settingsService.Save(settings);
    }

    public async Task SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        await TcpHostService.SendMessageAsync(message, _settingsService.Current, cancellationToken)
            .ConfigureAwait(false);
    }

    private async void HandleMessageReceivedAsync(ReceivedMessage message)
    {
        if (IsAway && !message.IsAutoReply && !string.IsNullOrWhiteSpace(Settings.AwayMessage))
        {
            try
            {
                var peer = _discoveryService.FindPeer(message.SenderId)
                           ?? new PeerInfo
                           {
                               MachineId = message.SenderId,
                               DisplayName = message.SenderName,
                               IpAddress = message.SenderIpAddress,
                               TcpPort = Settings.TcpPort
                           };

                await SendMessageAsync(new OutgoingMessage
                {
                    Recipient = peer,
                    Body = Settings.AwayMessage,
                    IsAutoReply = true
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-reply failed: {ex.Message}");
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
        _appCts.Dispose();
    }
}
