using System.Net;
using System.Net.Sockets;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Protocol;
using Ypopup.Core.Settings;

namespace Ypopup.Network.Discovery;

public sealed class DiscoveryService : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Func<bool> _isAwayProvider;
    private readonly Dictionary<string, PeerInfo> _peers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _peerLock = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _announceTask;
    private int _disposed;

    public event Action<IReadOnlyList<PeerInfo>>? PeersChanged;

    public DiscoveryService(SettingsService settingsService, Func<bool> isAwayProvider)
    {
        _settingsService = settingsService;
        _isAwayProvider = isAwayProvider;
    }

    public IReadOnlyList<PeerInfo> GetPeers()
    {
        lock (_peerLock)
        {
            PruneExpiredPeers();
            var settings = _settingsService.Current;

            return _peers.Values
                .Where(peer => !settings.OnlySameGroup
                               || string.IsNullOrWhiteSpace(settings.Group)
                               || string.Equals(peer.Group, settings.Group, StringComparison.OrdinalIgnoreCase))
                .OrderBy(peer => peer.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public PeerInfo? FindPeer(string machineId)
    {
        lock (_peerLock)
        {
            return _peers.TryGetValue(machineId, out var peer) ? peer : null;
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
            TcpPort = settings.TcpPort,
            Group = settings.Group,
            Email = settings.Email,
            Memo = settings.Memo,
            AdvertisedIp = LocalNetworkHelper.ResolvePreferredIp(settings.PreferredLocalIp),
            IsAway = _isAwayProvider()
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
            DisplayName = string.IsNullOrWhiteSpace(packet.SenderName)
                ? ResolvePeerIpAddress(packet, remoteEndPoint)
                : packet.SenderName,
            IpAddress = ResolvePeerIpAddress(packet, remoteEndPoint),
            TcpPort = packet.TcpPort > 0 ? packet.TcpPort : settings.TcpPort,
            Group = packet.Group,
            Email = packet.Email,
            Memo = packet.Memo,
            IsAway = packet.IsAway,
            LastSeenUtc = DateTime.UtcNow
        };

        var changed = false;
        lock (_peerLock)
        {
            if (!_peers.TryGetValue(peer.MachineId, out var existing) || HasPeerChanged(existing, peer))
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

    private static string ResolvePeerIpAddress(LanPacket packet, IPEndPoint remoteEndPoint)
    {
        if (!string.IsNullOrWhiteSpace(packet.AdvertisedIp)
            && IPAddress.TryParse(packet.AdvertisedIp, out var advertised)
            && advertised.AddressFamily == AddressFamily.InterNetwork)
        {
            return packet.AdvertisedIp;
        }

        return NetworkAddressHelper.NormalizeToConnectableAddress(remoteEndPoint.Address);
    }

    private static bool HasPeerChanged(PeerInfo existing, PeerInfo updated)
    {
        return existing.DisplayName != updated.DisplayName
               || existing.IpAddress != updated.IpAddress
               || existing.TcpPort != updated.TcpPort
               || existing.Group != updated.Group
               || existing.Email != updated.Email
               || existing.Memo != updated.Memo
               || existing.IsAway != updated.IsAway;
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
        var changed = false;
        lock (_peerLock)
        {
            var before = _peers.Count;
            PruneExpiredPeers();
            changed = _peers.Count != before;
        }

        if (changed)
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
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

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
