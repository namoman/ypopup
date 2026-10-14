using System.Collections.Concurrent;
using Ypopup.Core.Contracts;
using Ypopup.Core.Models;

namespace Ypopup.Network.Messaging;

public sealed class ChatRoomManager : IChatRoomManager
{
    private readonly IChatStorageRepository _storage;
    private readonly string _myUserId;
    private readonly ConcurrentDictionary<string, ChatRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public event Action<ChatRoom>? OnRoomCreatedOrUpdated;
    public event Action<string>? OnRoomRemoved;

    public ChatRoomManager(IChatStorageRepository storage, string myUserId)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _myUserId = myUserId ?? throw new ArgumentNullException(nameof(myUserId));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var stored = await _storage.LoadAllRoomsAsync(ct).ConfigureAwait(false);
        foreach (var r in stored)
        {
            _rooms[r.RoomId] = r;
        }
    }

    public async Task<ChatRoom> GetOrCreateDirectRoomAsync(string targetUserId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);

        var existing = _rooms.Values.FirstOrDefault(r =>
            r.Type == RoomType.Direct1on1 &&
            r.ParticipantIds.Contains(_myUserId, StringComparer.OrdinalIgnoreCase) &&
            r.ParticipantIds.Contains(targetUserId, StringComparer.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing;
        }

        var roomId = Guid.NewGuid().ToString();
        var room = new ChatRoom(
            RoomId: roomId,
            Title: targetUserId,
            Type: RoomType.Direct1on1,
            ParticipantIds: new[] { _myUserId, targetUserId },
            CreatedAtUtc: DateTime.UtcNow,
            LastMessageAtUtc: DateTime.UtcNow
        );

        _rooms[roomId] = room;
        await _storage.SaveRoomAsync(room, ct).ConfigureAwait(false);
        OnRoomCreatedOrUpdated?.Invoke(room);
        return room;
    }

    public async Task<ChatRoom> CreateGroupRoomAsync(string title, IEnumerable<string> participantIds, CancellationToken ct = default)
    {
        var participants = participantIds.ToList();
        if (!participants.Contains(_myUserId, StringComparer.OrdinalIgnoreCase))
        {
            participants.Add(_myUserId);
        }

        var roomId = Guid.NewGuid().ToString();
        var room = new ChatRoom(
            RoomId: roomId,
            Title: string.IsNullOrWhiteSpace(title) ? "새 그룹 대화" : title,
            Type: RoomType.GroupChat,
            ParticipantIds: participants,
            CreatedAtUtc: DateTime.UtcNow,
            LastMessageAtUtc: DateTime.UtcNow
        );

        _rooms[roomId] = room;
        await _storage.SaveRoomAsync(room, ct).ConfigureAwait(false);
        OnRoomCreatedOrUpdated?.Invoke(room);
        return room;
    }

    public Task<ChatRoom?> GetRoomAsync(string roomId, CancellationToken ct = default)
    {
        _rooms.TryGetValue(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<IReadOnlyList<ChatRoom>> GetActiveRoomsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ChatRoom> list = _rooms.Values.OrderByDescending(r => r.LastMessageAtUtc).ToList();
        return Task.FromResult(list);
    }

    public async Task LeaveRoomAsync(string roomId, CancellationToken ct = default)
    {
        if (_rooms.TryRemove(roomId, out _))
        {
            await _storage.DeleteRoomAsync(roomId, ct).ConfigureAwait(false);
            OnRoomRemoved?.Invoke(roomId);
        }
    }
}
