using Ypopup.Models;

namespace Ypopup.Services;

public sealed class YpopupCoordinator : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DiscoveryService _discoveryService;
    private readonly TcpHostService _tcpHostService;
    private readonly CancellationTokenSource _appCts = new();

    public event Action<IReadOnlyList<PeerInfo>>? PeersChanged;
    public event Action<ReceivedMessage>? MessageReceived;

    public YpopupCoordinator()
    {
        _settingsService = new SettingsService();
        _discoveryService = new DiscoveryService(_settingsService);
        _tcpHostService = new TcpHostService(_settingsService);

        _discoveryService.PeersChanged += peers => PeersChanged?.Invoke(peers);
        _tcpHostService.MessageReceived += message => MessageReceived?.Invoke(message);
    }

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

    public async ValueTask DisposeAsync()
    {
        await _appCts.CancelAsync().ConfigureAwait(false);
        await _discoveryService.DisposeAsync().ConfigureAwait(false);
        await _tcpHostService.DisposeAsync().ConfigureAwait(false);
        _appCts.Dispose();
    }
}
