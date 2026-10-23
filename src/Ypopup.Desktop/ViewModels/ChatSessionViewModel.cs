using System.Collections.ObjectModel;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Ypopup.Core.Contracts;
using Ypopup.Core.Logging;
using Ypopup.Core.Messaging;
using Ypopup.Core.Models;
using Ypopup.Network.Messaging;

namespace Ypopup.Desktop.ViewModels;

public sealed class MessageBubbleItem : ViewModelBase
{
    private static readonly IBrush MineBrush = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush OtherBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));

    public string MessageId { get; }
    public string SenderId { get; }
    public string SenderName { get; }
    public string Content { get; }
    public DateTime Time { get; }
    public bool IsMine { get; }
    public bool IsSystem { get; }
    public bool ShowSender => !IsMine && !IsSystem;
    public IReadOnlyList<FileAttachmentInfo> Attachments { get; }
    private int _readCount;

    public string FormattedTime => Time.ToString("tt h:mm");
    public HorizontalAlignment BubbleAlignment => IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public IBrush BubbleBackground => IsMine ? MineBrush : OtherBrush;
    public IBrush ContentForeground => IsMine ? Brushes.White : Brushes.Black;
    public int ReadCount => _readCount;
    public string ReadCountText => _readCount > 0 ? $"읽음 {_readCount}" : string.Empty;
    public bool ShowReadCount => !IsSystem && _readCount > 0;
    public IBrush ReadCountForeground => IsMine
        ? new SolidColorBrush(Color.Parse("#DBEAFE"))
        : new SolidColorBrush(Color.Parse("#64748B"));

    public void SetReadCount(int count)
    {
        if (!SetProperty(ref _readCount, count))
        {
            return;
        }

        OnPropertyChanged(nameof(ReadCountText));
        OnPropertyChanged(nameof(ShowReadCount));
    }

    public MessageBubbleItem(ChatMessage message, string myUserId)
    {
        MessageId = message.MessageId;
        SenderId = message.SenderId;
        SenderName = message.SenderName;
        Content = message.Content;
        Time = message.TimestampUtc.ToLocalTime();
        IsMine = string.Equals(SenderId, myUserId, StringComparison.OrdinalIgnoreCase);
        IsSystem = message.Type == MessageType.SystemNotice;
        Attachments = message.Attachments;
    }
}

public sealed class ChatSessionViewModel : ViewModelBase
{
    private readonly ChatRoom _room;
    private readonly List<string> _participantIds;
    private readonly string _myUserId;
    private readonly string _myDisplayName;
    private readonly IMessageDispatcher _dispatcher;
    private readonly IChatStorageRepository _storage;
    private readonly Func<IEnumerable<PeerUser>> _peersProvider;

    private string _inputText = string.Empty;

    public ChatRoom Room => _room;
    public string RoomTitle => _room.Title;
    public ObservableCollection<MessageBubbleItem> Messages { get; } = new();

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value))
            {
                OnPropertyChanged(nameof(CanSend));
            }
        }
    }

    public bool CanSend => !string.IsNullOrWhiteSpace(InputText);

    public ChatSessionViewModel(
        ChatRoom room,
        string myUserId,
        string myDisplayName,
        IMessageDispatcher dispatcher,
        IChatStorageRepository storage,
        Func<IEnumerable<PeerUser>> peersProvider)
    {
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _myUserId = myUserId ?? throw new ArgumentNullException(nameof(myUserId));
        _myDisplayName = myDisplayName ?? throw new ArgumentNullException(nameof(myDisplayName));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _peersProvider = peersProvider ?? throw new ArgumentNullException(nameof(peersProvider));
        _participantIds = room.ParticipantIds.ToList();
    }

    public void NoteParticipantLeft(string userId, string displayName)
    {
        _participantIds.RemoveAll(id => string.Equals(id, userId, StringComparison.OrdinalIgnoreCase));
        var name = string.IsNullOrWhiteSpace(displayName) ? "참여자" : displayName;
        AddIncomingMessage(new ChatMessage(
            MessageId: Guid.NewGuid().ToString(),
            RoomId: _room.RoomId,
            SenderId: userId,
            SenderName: name,
            Content: $"{name}님이 나갔습니다.",
            Type: MessageType.SystemNotice,
            TimestampUtc: DateTime.UtcNow));
    }

    public async Task LoadRecentMessagesAsync()
    {
        var list = await _storage.GetRecentMessagesAsync(_room.RoomId);
        var readers = await _storage.LoadReadersAsync(_room.RoomId);
        var seenByMe = new List<string>();
        Messages.Clear();
        foreach (var msg in list)
        {
            var item = new MessageBubbleItem(msg, _myUserId);
            readers.TryGetValue(msg.MessageId, out var readerIds);
            item.SetReadCount(ReadReceipt.CountOthers(readerIds, msg.SenderId));
            Messages.Add(item);
            if (!item.IsMine && !item.IsSystem)
            {
                seenByMe.Add(msg.MessageId);
            }
        }

        if (seenByMe.Count > 0)
        {
            await PublishReadAsync(seenByMe);
        }
    }

    public void AddIncomingMessage(ChatMessage msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = new MessageBubbleItem(msg, _myUserId);
            Messages.Add(item);
            if (!item.IsMine && !item.IsSystem)
            {
                _ = PublishReadAsync([msg.MessageId]);
            }
        });
    }

    public void NoteRead(string readerId, string? body)
    {
        if (string.IsNullOrWhiteSpace(readerId)
            || string.Equals(readerId, _myUserId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var ids = ReadReceipt.ReadMessageIds(body);
        if (ids.Count == 0)
        {
            return;
        }

        _ = SaveReadAsync(readerId, ids);
    }

    public async Task SendMessageAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        InputText = string.Empty;

        var msg = new ChatMessage(
            MessageId: Guid.NewGuid().ToString(),
            RoomId: _room.RoomId,
            SenderId: _myUserId,
            SenderName: _myDisplayName,
            Content: text,
            Type: MessageType.Text,
            TimestampUtc: DateTime.UtcNow
        );

        // 로컬 UI 및 저장소 반영
        Messages.Add(new MessageBubbleItem(msg, _myUserId));
        await _storage.AppendMessageAsync(msg);

        // 네트워크 디스패치
        var allPeers = _peersProvider();
        var targetPeers = allPeers.Where(p => _participantIds.Contains(p.UserId, StringComparer.OrdinalIgnoreCase)
                                              && !string.Equals(p.UserId, _myUserId, StringComparison.OrdinalIgnoreCase)).ToList();

        var packet = new ExtendedLanPacket
        {
            Type = ExtendedPacketType.GroupChatMessage,
            MessageId = msg.MessageId,
            RoomId = _room.RoomId,
            SenderId = _myUserId,
            SenderName = _myDisplayName,
            Body = text,
            ParticipantIds = _participantIds.ToList()
        };

        _ = _dispatcher.DispatchRoomMessageAsync(_room.RoomId, packet, targetPeers);
    }

    private async Task PublishReadAsync(IReadOnlyList<string> messageIds)
    {
        try
        {
            await SaveReadAsync(_myUserId, messageIds);
            var targets = _peersProvider()
                .Where(peer => _participantIds.Contains(peer.UserId, StringComparer.OrdinalIgnoreCase)
                               && !string.Equals(peer.UserId, _myUserId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            var packet = new ExtendedLanPacket
            {
                Type = ExtendedPacketType.MessageRead,
                RoomId = _room.RoomId,
                SenderId = _myUserId,
                SenderName = _myDisplayName,
                Body = ReadReceipt.WriteMessageIds(messageIds),
                ParticipantIds = _participantIds.ToList()
            };
            await _dispatcher.SendPacketAsync(packet, targets);
        }
        catch (Exception ex)
        {
            LogService.Warning("Chat", $"읽음 전송 실패: {ex.Message}");
        }
    }

    private async Task SaveReadAsync(string readerId, IReadOnlyList<string> messageIds)
    {
        try
        {
            await _storage.AddReadersAsync(_room.RoomId, messageIds, readerId);
            var readers = await _storage.LoadReadersAsync(_room.RoomId);
            await Dispatcher.UIThread.InvokeAsync(() => ApplyReadCounts(readers));
        }
        catch (Exception ex)
        {
            LogService.Warning("Chat", $"읽음 저장 실패: {ex.Message}");
        }
    }

    private void ApplyReadCounts(IReadOnlyDictionary<string, IReadOnlyList<string>> readers)
    {
        foreach (var item in Messages)
        {
            if (item.IsSystem)
            {
                continue;
            }

            readers.TryGetValue(item.MessageId, out var readerIds);
            item.SetReadCount(ReadReceipt.CountOthers(readerIds, item.SenderId));
        }
    }
}
