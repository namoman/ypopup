using Ypopup.Core.Models;

namespace Ypopup.Core.Contracts;

public interface IChatRoomManager
{
    Task<ChatRoom> GetOrCreateDirectRoomAsync(string targetUserId, CancellationToken ct = default);
    Task<ChatRoom> CreateGroupRoomAsync(string title, IEnumerable<string> participantIds, CancellationToken ct = default);
    Task<ChatRoom?> GetRoomAsync(string roomId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatRoom>> GetActiveRoomsAsync(CancellationToken ct = default);
    Task LeaveRoomAsync(string roomId, CancellationToken ct = default);

    event Action<ChatRoom>? OnRoomCreatedOrUpdated;
    event Action<string>? OnRoomRemoved;
}

public interface IGroupManager
{
    Task<IReadOnlyList<UserGroup>> GetAllGroupsAsync(CancellationToken ct = default);
    Task<UserGroup> CreateGroupAsync(string groupName, CancellationToken ct = default);
    Task DeleteGroupAsync(string groupId, CancellationToken ct = default);
    Task AssignUserToGroupAsync(string groupId, string userId, CancellationToken ct = default);
    Task RemoveUserFromGroupAsync(string groupId, string userId, CancellationToken ct = default);
    Task ReorderGroupsAsync(IEnumerable<string> orderedGroupIds, CancellationToken ct = default);
    Task SetGroupExpandedAsync(string groupId, bool isExpanded, CancellationToken ct = default);

    event Action? OnGroupsChanged;
}

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
