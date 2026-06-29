namespace Ypopup.Models;

public enum PacketType
{
    Announce,
    TextMessage,
    FileData
}

public sealed class LanPacket
{
    public PacketType Type { get; set; }
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public List<FileAttachmentInfo> Attachments { get; set; } = [];
    public FileAttachmentInfo? File { get; set; }
}

public sealed class FileAttachmentInfo
{
    public string FileId { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
}

public sealed class PeerInfo
{
    public required string MachineId { get; init; }
    public required string DisplayName { get; set; }
    public required string IpAddress { get; set; }
    public int TcpPort { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    public string EndpointKey => $"{IpAddress}:{TcpPort}";
}

public sealed class ReceivedMessage
{
    public required string MessageId { get; init; }
    public required string SenderId { get; init; }
    public required string SenderName { get; init; }
    public required string Body { get; init; }
    public List<string> SavedFilePaths { get; init; } = [];
    public DateTime ReceivedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class OutgoingMessage
{
    public required PeerInfo Recipient { get; init; }
    public string Body { get; set; } = string.Empty;
    public List<string> AttachmentPaths { get; set; } = [];
}
