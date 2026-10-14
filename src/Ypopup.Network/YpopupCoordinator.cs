using Ypopup.Core.Contracts;
using Ypopup.Core.Helpers;
using Ypopup.Core.Logging;
using Ypopup.Core.Models;
using Ypopup.Core.Sharing;
using Ypopup.Core.Settings;
using Ypopup.Core.Storage;
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
    private readonly JsonChatStorageRepository _chatStorage;
    private readonly PacketRouter _packetRouter;
    private readonly MessageDispatcher _messageDispatcher;
    private readonly ChatRoomManager _chatRoomManager;
    private readonly GroupManager _groupManager;
    private readonly CancellationTokenSource _appCts = new();
    private readonly Dictionary<string, DateTime> _lastAutoReplyTimes = new(StringComparer.OrdinalIgnoreCase);
    private int _disposed;

    public SharedFolderHostStartResult SharedFolderHostStatus { get; private set; } = new(false);

    public event Action<IReadOnlyList<PeerInfo>>? PeersChanged;
    public event Action<ReceivedMessage>? MessageReceived;
    public event Action? SettingsSaved;
    public event Action<ChatRoom>? GroupRoomOpened;

    public YpopupCoordinator()
    {
        _settingsService = new SettingsService();
        _discoveryService = new DiscoveryService(
            _settingsService,
            () => IsAway,
            () => _sharedFolderHostService.IsRunning);
        _tcpHostService = new TcpHostService(_settingsService);
        _sharedFolderHostService = new SharedFolderHostService(_settingsService);

        _chatStorage = new JsonChatStorageRepository();
        _packetRouter = new PacketRouter();
        _messageDispatcher = new MessageDispatcher();
        _chatRoomManager = new ChatRoomManager(_chatStorage, _settingsService.Current.MachineId);
        _groupManager = new GroupManager(_chatStorage);

        _discoveryService.PeersChanged += peers => PeersChanged?.Invoke(peers);
        _tcpHostService.MessageReceived += HandleMessageReceivedAsync;
        _tcpHostService.ExtendedPacketReceived += HandleExtendedPacketReceived;
        _packetRouter.OnUnregisteredRoomPacketReceived += packet =>
            _ = BackgroundTaskTracker.RunAsync("미등록 룸 패킷", () => HandleUnregisteredRoomPacketAsync(packet));
        _packetRouter.OnDirectMessageReceived += HandleExtendedDirectNotice;
    }

    public bool IsAway { get; set; }

    public AppSettings Settings => _settingsService.Current;

    public IChatRoomManager ChatRooms => _chatRoomManager;
    public IGroupManager Groups => _groupManager;
    public IChatStorageRepository ChatStorage => _chatStorage;
    public IMessageDispatcher MessageDispatcher => _messageDispatcher;
    public IPacketRouter PacketRouter => _packetRouter;

    public IReadOnlyList<PeerInfo> GetPeers() => _discoveryService.GetPeers();

    public DateTime LastAnnounceSentUtc => _discoveryService.LastAnnounceSentUtc;
    public DateTime LastPacketReceivedUtc => _discoveryService.LastPacketReceivedUtc;

    public async Task StartAsync()
    {
        await _chatRoomManager.InitializeAsync(_appCts.Token).ConfigureAwait(false);
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

    public IEnumerable<PeerUser> GetPeerUsers()
        => GetPeers().Select(ToPeerUser);

    public static PeerUser ToPeerUser(PeerInfo peer)
        => new(
            peer.MachineId,
            peer.DisplayName,
            peer.IpAddress,
            peer.TcpPort,
            peer.Group,
            peer.IsAway,
            peer.LastSeenUtc);

    public async Task<ChatRoom> CreateGroupRoomAndInviteAsync(
        string title,
        IReadOnlyList<PeerInfo> participants,
        CancellationToken cancellationToken = default)
    {
        var participantIds = participants
            .Select(p => p.MachineId)
            .Append(Settings.MachineId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var room = await _chatRoomManager.CreateGroupRoomAsync(title, participantIds, cancellationToken)
            .ConfigureAwait(false);

        var invite = new ExtendedLanPacket
        {
            Type = ExtendedPacketType.RoomInvite,
            RoomId = room.RoomId,
            RoomTitle = room.Title,
            SenderId = Settings.MachineId,
            SenderName = Settings.DisplayName,
            ParticipantIds = participantIds,
            Body = $"{Settings.DisplayName}님이 «{room.Title}» 단톡방에 초대했습니다."
        };

        var targets = participants
            .Where(p => !string.Equals(p.MachineId, Settings.MachineId, StringComparison.OrdinalIgnoreCase))
            .Select(ToPeerUser)
            .ToList();

        await _messageDispatcher.SendPacketAsync(invite, targets, cancellationToken).ConfigureAwait(false);

        GroupRoomOpened?.Invoke(room);
        return room;
    }

    public async Task<IReadOnlyList<SendResult>> SendMulticastNoticeAsync(
        string body,
        IReadOnlyList<PeerInfo> recipients,
        CancellationToken cancellationToken = default)
    {
        var packet = new ExtendedLanPacket
        {
            Type = ExtendedPacketType.MultiCastDirectNotice,
            SenderId = Settings.MachineId,
            SenderName = Settings.DisplayName,
            Body = body
        };

        var targets = recipients
            .Where(p => !string.Equals(p.MachineId, Settings.MachineId, StringComparison.OrdinalIgnoreCase))
            .Select(ToPeerUser);

        return await _messageDispatcher.MultiCastNoticeAsync(packet, targets, cancellationToken)
            .ConfigureAwait(false);
    }

    private void HandleExtendedPacketReceived(ExtendedLanPacket packet)
        => _packetRouter.RouteIncomingPacket(packet);

    private void HandleExtendedDirectNotice(ExtendedLanPacket packet)
    {
        if (packet.Type != ExtendedPacketType.MultiCastDirectNotice
            && packet.Type != ExtendedPacketType.RoomInvite)
        {
            return;
        }

        if (packet.Type == ExtendedPacketType.RoomInvite && !string.IsNullOrWhiteSpace(packet.RoomId))
        {
            _ = BackgroundTaskTracker.RunAsync("룸 초대 처리", () => AcceptRoomInviteAsync(packet));
            return;
        }

        MessageReceived?.Invoke(new ReceivedMessage
        {
            MessageId = packet.MessageId,
            SenderId = packet.SenderId,
            SenderName = packet.SenderName,
            SenderIpAddress = ResolvePeerIp(packet.SenderId),
            Body = string.IsNullOrWhiteSpace(packet.Body)
                ? "(단체 쪽지)"
                : $"[단체 쪽지] {packet.Body}",
            SavedFilePaths = [],
            IsAutoReply = false
        });
    }

    private async Task AcceptRoomInviteAsync(ExtendedLanPacket packet)
    {
        var room = new ChatRoom(
            RoomId: packet.RoomId!,
            Title: string.IsNullOrWhiteSpace(packet.RoomTitle) ? "그룹 대화" : packet.RoomTitle!,
            Type: RoomType.GroupChat,
            ParticipantIds: packet.ParticipantIds.Count > 0
                ? packet.ParticipantIds
                : new[] { Settings.MachineId, packet.SenderId });

        await _chatRoomManager.UpsertRoomAsync(room, _appCts.Token).ConfigureAwait(false);
        GroupRoomOpened?.Invoke(room);
    }

    private async Task HandleUnregisteredRoomPacketAsync(ExtendedLanPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.RoomId))
        {
            return;
        }

        if (packet.Type == ExtendedPacketType.RoomInvite)
        {
            await AcceptRoomInviteAsync(packet).ConfigureAwait(false);
            return;
        }

        var existing = await _chatRoomManager.GetRoomAsync(packet.RoomId, _appCts.Token).ConfigureAwait(false);
        if (existing is null)
        {
            var room = new ChatRoom(
                RoomId: packet.RoomId,
                Title: string.IsNullOrWhiteSpace(packet.RoomTitle) ? "그룹 대화" : packet.RoomTitle!,
                Type: RoomType.GroupChat,
                ParticipantIds: packet.ParticipantIds.Count > 0
                    ? packet.ParticipantIds
                    : new[] { Settings.MachineId, packet.SenderId });
            existing = await _chatRoomManager.UpsertRoomAsync(room, _appCts.Token).ConfigureAwait(false);
        }

        if (packet.Type == ExtendedPacketType.GroupChatMessage)
        {
            var msg = new ChatMessage(
                MessageId: packet.MessageId,
                RoomId: packet.RoomId,
                SenderId: packet.SenderId,
                SenderName: packet.SenderName,
                Content: packet.Body,
                Type: MessageType.Text,
                TimestampUtc: packet.SentAtUtc == default ? DateTime.UtcNow : packet.SentAtUtc);

            await _chatStorage.AppendMessageAsync(msg, _appCts.Token).ConfigureAwait(false);
            GroupRoomOpened?.Invoke(existing);
        }
    }

    private string ResolvePeerIp(string senderId)
    {
        var peer = _discoveryService.FindPeer(senderId);
        return peer?.IpAddress ?? "0.0.0.0";
    }

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
