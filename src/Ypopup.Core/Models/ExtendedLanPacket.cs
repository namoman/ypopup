namespace Ypopup.Core.Models;

public enum ExtendedPacketType
{
    Announce = 0,
    TextMessage = 1,
    FileData = 2,
    GroupChatMessage = 10,
    RoomInvite = 11,
    RoomLeave = 12,
    MultiCastDirectNotice = 13
}

public sealed class ExtendedLanPacket
{
    public ExtendedPacketType Type { get; set; } = ExtendedPacketType.TextMessage;
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public string Group { get; set; } = string.Empty;
    public List<FileAttachmentInfo> Attachments { get; set; } = [];
    public string? RoomId { get; set; }
    public string? RoomTitle { get; set; }
    public List<string> ParticipantIds { get; set; } = [];
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
