using System.Net;
using System.Net.Sockets;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Core.Network;
using Ypopup.Core.Protocol;
using Ypopup.Core.Settings;

namespace Ypopup.Network.Discovery;

public sealed class DiscoveryService : IAsyncDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Func<bool> _isAwayProvider;
    private readonly Func<bool> _isShareFolderHostRunning;
    private readonly Dictionary<string, PeerInfo> _peers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _peerLock = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _announceTask;
    private int _disposed;
    private int _multicastJoined;
    private long _lastAnnounceSentTicks;
    private long _lastPacketReceivedTicks;

    public DateTime LastAnnounceSentUtc => new(Interlocked.Read(ref _lastAnnounceSentTicks));
    public DateTime LastPacketReceivedUtc => new(Interlocked.Read(ref _lastPacketReceivedTicks));
    public string MulticastStatus { get; private set; } = $"{AppConstants.DiscoveryMulticastAddress} (대기)";

    public event Action<IReadOnlyList<PeerInfo>>? PeersChanged;

    public DiscoveryService(
        SettingsService settingsService,
        Func<bool> isAwayProvider,
        Func<bool>? isShareFolderHostRunning = null)
    {
        _settingsService = settingsService;
        _isAwayProvider = isAwayProvider;
        _isShareFolderHostRunning = isShareFolderHostRunning ?? (() => false);
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
        await StopAsync().ConfigureAwait(false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _udpClient = new UdpClient(_settingsService.Current.DiscoveryPort)
        {
            EnableBroadcast = true
        };
        JoinMulticast(_udpClient);

        _listenTask = ListenLoopAsync(_cts.Token);
        _announceTask = AnnounceLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_udpClient is null)
        {
            return;
        }

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        LeaveMulticast(_udpClient);
        _udpClient.Dispose();
        _udpClient = null;

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

        _listenTask = null;
        _announceTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
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
                LogService.Warning("Discovery", $"Listen error: {ex.Message}");
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
                LogService.Warning("Discovery", $"Announce error: {ex.Message}");
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
        var shareFolderActive = settings.ShareFolderEnabled && _isShareFolderHostRunning();
        var packet = new LanPacket
        {
            Type = PacketType.Announce,
            SenderId = settings.MachineId,
            SenderName = settings.DisplayName,
            TcpPort = settings.TcpPort,
            Group = settings.Group,
            JobTitle = settings.JobTitle,
            Email = settings.Email,
            Memo = settings.Memo,
            AvatarHash = AvatarStore.CurrentHash(),
            AdvertisedIp = LocalNetworkHelper.ResolvePreferredIp(settings.PreferredLocalIp),
            IsAway = _isAwayProvider(),
            ShareFolderEnabled = shareFolderActive,
            ShareFolderPort = shareFolderActive ? settings.ShareFolderPort : 0
        };

        Interlocked.Exchange(ref _lastAnnounceSentTicks, DateTime.UtcNow.Ticks);

        var payload = PacketCodec.Serialize(packet);
        var targets = DiscoveryAnnounceTargets.Build(
            LocalNetworkHelper.GetLocalSubnetBroadcastAddresses(),
            settings.DiscoveryPort);

        foreach (var endpoint in targets)
        {
            try
            {
                _udpClient.Send(payload, payload.Length, endpoint);
            }
            catch (Exception ex)
            {
                LogService.Warning("Discovery", $"Send announce to {endpoint.Address}: {ex.Message}");
            }
        }
    }

    private void JoinMulticast(UdpClient client)
    {
        var localText = LocalNetworkHelper.ResolvePreferredIp(_settingsService.Current.PreferredLocalIp);
        if (!IPAddress.TryParse(localText, out var local) || IPAddress.IsLoopback(local))
        {
            MulticastStatus = $"{AppConstants.DiscoveryMulticastAddress} (건너뜀)";
            return;
        }

        try
        {
            // TTL 1: 그룹 알림이 첫 라우터를 넘지 않게 한다.
            client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
            client.JoinMulticastGroup(DiscoveryAnnounceTargets.MulticastGroup, local);
            _multicastJoined = 1;
            MulticastStatus = $"{AppConstants.DiscoveryMulticastAddress} (가입됨)";
        }
        catch (Exception ex)
        {
            _multicastJoined = 0;
            MulticastStatus = $"{AppConstants.DiscoveryMulticastAddress} (가입 실패)";
            LogService.Warning("Discovery", $"Multicast join {localText}: {ex.Message}");
        }
    }

    private void LeaveMulticast(UdpClient client)
    {
        if (Interlocked.Exchange(ref _multicastJoined, 0) == 0)
        {
            return;
        }

        try
        {
            client.DropMulticastGroup(DiscoveryAnnounceTargets.MulticastGroup);
        }
        catch (Exception ex)
        {
            LogService.Warning("Discovery", $"Multicast leave: {ex.Message}");
        }
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

        Interlocked.Exchange(ref _lastPacketReceivedTicks, DateTime.UtcNow.Ticks);

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
            JobTitle = packet.JobTitle,
            Email = packet.Email,
            Memo = packet.Memo,
            AvatarHash = packet.AvatarHash,
            IsAway = packet.IsAway,
            ShareFolderEnabled = packet.ShareFolderEnabled,
            ShareFolderPort = packet.ShareFolderPort,
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
               || existing.JobTitle != updated.JobTitle
               || existing.Email != updated.Email
               || existing.Memo != updated.Memo
               || existing.AvatarHash != updated.AvatarHash
               || existing.IsAway != updated.IsAway
               || existing.ShareFolderEnabled != updated.ShareFolderEnabled
               || existing.ShareFolderPort != updated.ShareFolderPort;
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

        await StopAsync().ConfigureAwait(false);
    }
}
