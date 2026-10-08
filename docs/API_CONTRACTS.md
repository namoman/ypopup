# Y-popup API 계약 명세서 (API_CONTRACTS.md)

## 1. Core 도메인 모델 (Entities & Enums)

```csharp
namespace Ypopup.Core.Models;

public enum RoomType
{
    Direct1on1 = 0,
    GroupChat = 1
}

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

public sealed record UserGroup(
    string GroupId,
    string GroupName,
    IReadOnlyList<string> MemberUserIds,
    int SortOrder = 0,
    bool IsExpanded = true
);

public sealed record ChatRoom(
    string RoomId,
    string Title,
    RoomType Type,
    IReadOnlyList<string> ParticipantIds,
    DateTime CreatedAtUtc,
    DateTime LastMessageAtUtc
);

public sealed record ChatMessage(
    string MessageId,
    string RoomId,
    string SenderId,
    string SenderName,
    string Content,
    MessageType Type,
    DateTime TimestampUtc,
    IReadOnlyList<FileAttachmentInfo> Attachments
);
```

---

## 2. 네트워크 패킷 확장 규격 (Network Packet DTO)

기존 `LanPacket`의 하위 호환성을 유지하며 확장 필드를 정의합니다.

```csharp
namespace Ypopup.Core.Models;

public enum ExtendedPacketType
{
    Announce = 0,
    TextMessage = 1,
    FileData = 2,
    
    // 신규 추가 규격
    GroupChatMessage = 10,
    RoomInvite = 11,
    RoomLeave = 12,
    MultiCastDirectNotice = 13
}

public sealed class ExtendedLanPacket
{
    // 기존 호환 필드
    public ExtendedPacketType Type { get; set; } = ExtendedPacketType.TextMessage;
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public string Group { get; set; } = string.Empty;
    public List<FileAttachmentInfo> Attachments { get; set; } = [];

    // 멀티챗 & 다자간 세션 확장 필드
    public string? RoomId { get; set; }
    public string? RoomTitle { get; set; }
    public List<string> ParticipantIds { get; set; } = [];
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
```

---

## 3. 핵심 서비스 계약 인터페이스 (Service Contracts)

### 3.1 IChatRoomManager
```csharp
namespace Ypopup.Core.Contracts;

public interface IChatRoomManager
{
    Task<ChatRoom> GetOrCreateDirectRoomAsync(string targetUserId, CancellationToken ct = default);
    Task<ChatRoom> CreateGroupRoomAsync(string title, IEnumerable<string> participantIds, CancellationToken ct = default);
    Task<ChatRoom?> GetRoomAsync(string roomId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatRoom>> GetActiveRoomsAsync(CancellationToken ct = default);
    Task LeaveRoomAsync(string roomId, CancellationToken ct = default);
    
    event Action<ChatRoom> OnRoomCreatedOrUpdated;
    event Action<string> OnRoomRemoved;
}
```

### 3.2 IGroupManager
```csharp
namespace Ypopup.Core.Contracts;

public interface IGroupManager
{
    Task<IReadOnlyList<UserGroup>> GetAllGroupsAsync(CancellationToken ct = default);
    Task<UserGroup> CreateGroupAsync(string groupName, CancellationToken ct = default);
    Task DeleteGroupAsync(string groupId, CancellationToken ct = default);
    Task AssignUserToGroupAsync(string groupId, string userId, CancellationToken ct = default);
    Task RemoveUserFromGroupAsync(string groupId, string userId, CancellationToken ct = default);
    Task ReorderGroupsAsync(IEnumerable<string> orderedGroupIds, CancellationToken ct = default);
    Task SetGroupExpandedAsync(string groupId, bool isExpanded, CancellationToken ct = default);

    event Action OnGroupsChanged;
}
```

### 3.3 IPacketRouter
```csharp
namespace Ypopup.Core.Contracts;

public interface IPacketRouter
{
    void RegisterRoomHandler(string roomId, Action<ExtendedLanPacket> handler);
    void UnregisterRoomHandler(string roomId);
    void RouteIncomingPacket(ExtendedLanPacket packet);
    
    event Action<ExtendedLanPacket> OnUnregisteredRoomPacketReceived;
    event Action<ExtendedLanPacket> OnDirectMessageReceived;
}
```

### 3.4 IMessageDispatcher
```csharp
namespace Ypopup.Core.Contracts;

public sealed record SendResult(string PeerId, bool IsSuccess, string? ErrorMessage = null);

public interface IMessageDispatcher
{
    Task<IReadOnlyList<SendResult>> DispatchRoomMessageAsync(
        string roomId,
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default);

    Task<IReadOnlyList<SendResult>> MultiCastNoticeAsync(
        ExtendedLanPacket packet,
        IEnumerable<PeerUser> targetPeers,
        CancellationToken ct = default);
}
```

### 3.5 IChatStorageRepository
```csharp
namespace Ypopup.Core.Contracts;

public interface IChatStorageRepository
{
    Task SaveRoomAsync(ChatRoom room, CancellationToken ct = default);
    Task<ChatRoom?> LoadRoomAsync(string roomId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatRoom>> LoadAllRoomsAsync(CancellationToken ct = default);
    Task DeleteRoomAsync(string roomId, CancellationToken ct = default);

    Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(string roomId, int count = 50, CancellationToken ct = default);

    Task SaveGroupsAsync(IEnumerable<UserGroup> groups, CancellationToken ct = default);
    Task<IReadOnlyList<UserGroup>> LoadGroupsAsync(CancellationToken ct = default);
}
```
