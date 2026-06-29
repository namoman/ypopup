using System.Net;
using System.Net.Sockets;
using System.Text;
using Ypopup.Models;
using Ypopup.Protocol;

namespace Ypopup.Services;

public sealed class DiscoveryService : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<string, PeerInfo> _peers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _peerLock = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _announceTask;

    public event Action<IReadOnlyList<PeerInfo>>? PeersChanged;

    public DiscoveryService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<PeerInfo> GetPeers()
    {
        lock (_peerLock)
        {
            PruneExpiredPeers();
            return _peers.Values
                .OrderBy(peer => peer.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _udpClient = new UdpClient(_settingsService.Current.DiscoveryPort)
        {
            EnableBroadcast = true
        };

        _listenTask = ListenLoopAsync(_cts.Token);
        _announceTask = AnnounceLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _udpClient is not null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                HandleAnnounce(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery listen error: {ex.Message}");
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task AnnounceLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                BroadcastAnnounce();
                PruneAndNotify();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery announce error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }
    }

    private void BroadcastAnnounce()
    {
        if (_udpClient is null)
        {
            return;
        }

        var settings = _settingsService.Current;
        var packet = new LanPacket
        {
            Type = PacketType.Announce,
            SenderId = settings.MachineId,
            SenderName = settings.DisplayName,
            TcpPort = settings.TcpPort
        };

        var payload = PacketCodec.Serialize(packet);
        var endpoint = new IPEndPoint(IPAddress.Broadcast, settings.DiscoveryPort);
        _udpClient.Send(payload, payload.Length, endpoint);
    }

    private void HandleAnnounce(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        LanPacket packet;
        try
        {
            packet = PacketCodec.Deserialize(buffer);
        }
        catch (Exception)
        {
            return;
        }

        if (packet.Type != PacketType.Announce)
        {
            return;
        }

        var settings = _settingsService.Current;
        if (string.Equals(packet.SenderId, settings.MachineId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var peer = new PeerInfo
        {
            MachineId = packet.SenderId,
            DisplayName = string.IsNullOrWhiteSpace(packet.SenderName) ? remoteEndPoint.Address.ToString() : packet.SenderName,
            IpAddress = remoteEndPoint.Address.ToString(),
            TcpPort = packet.TcpPort > 0 ? packet.TcpPort : settings.TcpPort,
            LastSeenUtc = DateTime.UtcNow
        };

        var changed = false;
        lock (_peerLock)
        {
            if (!_peers.TryGetValue(peer.MachineId, out var existing)
                || existing.DisplayName != peer.DisplayName
                || existing.IpAddress != peer.IpAddress
                || existing.TcpPort != peer.TcpPort)
            {
                changed = true;
            }

            _peers[peer.MachineId] = peer;
        }

        if (changed)
        {
            NotifyPeersChanged();
        }
    }

    private void PruneExpiredPeers()
    {
        var expired = _peers
            .Where(pair => DateTime.UtcNow - pair.Value.LastSeenUtc > TimeSpan.FromSeconds(15))
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expired)
        {
            _peers.Remove(key);
        }
    }

    private void PruneAndNotify()
    {
        var before = _peers.Count;
        lock (_peerLock)
        {
            PruneExpiredPeers();
        }

        if (_peers.Count != before)
        {
            NotifyPeersChanged();
        }
    }

    private void NotifyPeersChanged()
    {
        PeersChanged?.Invoke(GetPeers());
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_announceTask is not null)
        {
            try
            {
                await _announceTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _udpClient?.Dispose();
        _cts?.Dispose();
    }
}
