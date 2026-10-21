using System.Collections.ObjectModel;
using Avalonia.Layout;
using Avalonia.Media;
using Ypopup.Core.Contracts;
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

    public string FormattedTime => Time.ToString("tt h:mm");
    public HorizontalAlignment BubbleAlignment => IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public IBrush BubbleBackground => IsMine ? MineBrush : OtherBrush;
    public IBrush ContentForeground => IsMine ? Brushes.White : Brushes.Black;

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
        Messages.Clear();
        foreach (var msg in list)
        {
            Messages.Add(new MessageBubbleItem(msg, _myUserId));
        }
    }

    public void AddIncomingMessage(ChatMessage msg)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Messages.Add(new MessageBubbleItem(msg, _myUserId));
        });
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
}
