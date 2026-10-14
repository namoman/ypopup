namespace Ypopup.Core.Models;

public enum MessageType
{
    Text = 0,
    Notice = 1,
    FileAttachment = 2,
    SystemNotice = 3
}

public sealed record PeerUser(
    string UserId,
    string DisplayName,
    string IpAddress,
    int TcpPort,
    string Group,
    bool IsAway,
    DateTime LastSeenUtc
);

public sealed record ChatMessage(
    string MessageId,
    string RoomId,
    string SenderId,
    string SenderName,
    string Content,
    MessageType Type,
    DateTime TimestampUtc,
    IReadOnlyList<FileAttachmentInfo>? Attachments = null
)
{
    public IReadOnlyList<FileAttachmentInfo> Attachments { get; init; } = Attachments ?? Array.Empty<FileAttachmentInfo>();
}
