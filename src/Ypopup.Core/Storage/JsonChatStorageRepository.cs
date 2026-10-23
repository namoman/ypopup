using System.Text.Encodings.Web;
using System.Text.Json;
using Ypopup.Core.Contracts;
using Ypopup.Core.Models;

namespace Ypopup.Core.Storage;

public sealed class JsonChatStorageRepository : IChatStorageRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _basePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonChatStorageRepository(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Y-popup",
            "storage");

        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(Path.Combine(_basePath, "messages"));
        Directory.CreateDirectory(Path.Combine(_basePath, "reads"));
    }

    private string RoomsFilePath => Path.Combine(_basePath, "rooms.json");
    private string LeftRoomsFilePath => Path.Combine(_basePath, "left-rooms.json");
    private string GroupsFilePath => Path.Combine(_basePath, "groups.json");
    private string GetMessageFilePath(string roomId) => Path.Combine(_basePath, "messages", $"{roomId}.jsonl");
    private string GetReadersFilePath(string roomId) => Path.Combine(_basePath, "reads", $"{roomId}.json");

    public async Task SaveRoomAsync(ChatRoom room, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var rooms = await LoadAllRoomsInternalAsync(ct).ConfigureAwait(false);
            var dict = rooms.ToDictionary(r => r.RoomId, StringComparer.OrdinalIgnoreCase);
            dict[room.RoomId] = room;

            var json = JsonSerializer.Serialize(dict.Values.ToList(), JsonOptions);
            await File.WriteAllTextAsync(RoomsFilePath, json, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChatRoom?> LoadRoomAsync(string roomId, CancellationToken ct = default)
    {
        var rooms = await LoadAllRoomsAsync(ct).ConfigureAwait(false);
        return rooms.FirstOrDefault(r => string.Equals(r.RoomId, roomId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ChatRoom>> LoadAllRoomsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await LoadAllRoomsInternalAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ChatRoom>> LoadAllRoomsInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(RoomsFilePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(RoomsFilePath, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ChatRoom>>(json, JsonOptions) ?? [];
    }

    public async Task DeleteRoomAsync(string roomId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var rooms = await LoadAllRoomsInternalAsync(ct).ConfigureAwait(false);
            var updated = rooms.Where(r => !string.Equals(r.RoomId, roomId, StringComparison.OrdinalIgnoreCase)).ToList();
            var json = JsonSerializer.Serialize(updated, JsonOptions);
            await File.WriteAllTextAsync(RoomsFilePath, json, ct).ConfigureAwait(false);

            var msgFile = GetMessageFilePath(roomId);
            if (File.Exists(msgFile))
            {
                File.Delete(msgFile);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MarkRoomLeftAsync(string roomId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var left = await LoadLeftRoomsInternalAsync(ct).ConfigureAwait(false);
            left.Add(roomId);
            await SaveLeftRoomsInternalAsync(left, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearRoomLeftAsync(string roomId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var left = await LoadLeftRoomsInternalAsync(ct).ConfigureAwait(false);
            if (left.Remove(roomId))
            {
                await SaveLeftRoomsInternalAsync(left, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IsRoomLeftAsync(string roomId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var left = await LoadLeftRoomsInternalAsync(ct).ConfigureAwait(false);
            return left.Contains(roomId);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<HashSet<string>> LoadLeftRoomsInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(LeftRoomsFilePath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await File.ReadAllTextAsync(LeftRoomsFilePath, ct).ConfigureAwait(false);
        var ids = string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    }

    private Task SaveLeftRoomsInternalAsync(HashSet<string> left, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(left.ToList(), JsonOptions);
        return File.WriteAllTextAsync(LeftRoomsFilePath, json, ct);
    }

    public async Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var line = JsonSerializer.Serialize(message, JsonOptions).Replace("\r", "").Replace("\n", "");
            var path = GetMessageFilePath(message.RoomId);
            await File.AppendAllLinesAsync(path, [line], ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(string roomId, int count = 50, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetMessageFilePath(roomId);
            if (!File.Exists(path))
            {
                return [];
            }

            var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
            var messages = new List<ChatMessage>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var msg = JsonSerializer.Deserialize<ChatMessage>(line, JsonOptions);
                    if (msg is not null)
                    {
                        messages.Add(msg);
                    }
                }
                catch
                {
                    // Ignore corrupted lines
                }
            }

            return messages.TakeLast(count).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddReadersAsync(
        string roomId,
        IReadOnlyCollection<string> messageIds,
        string readerId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId)
            || string.IsNullOrWhiteSpace(readerId)
            || messageIds.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var map = await LoadReadersInternalAsync(roomId, ct).ConfigureAwait(false);
            var changed = false;
            foreach (var messageId in messageIds)
            {
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    continue;
                }

                if (!map.TryGetValue(messageId, out var readers))
                {
                    readers = [];
                    map[messageId] = readers;
                }

                if (readers.Any(id => string.Equals(id, readerId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                readers.Add(readerId);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            var json = JsonSerializer.Serialize(map, JsonOptions);
            await File.WriteAllTextAsync(GetReadersFilePath(roomId), json, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadReadersAsync(
        string roomId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var map = await LoadReadersInternalAsync(roomId, ct).ConfigureAwait(false);
            return map.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, List<string>>> LoadReadersInternalAsync(string roomId, CancellationToken ct)
    {
        var path = GetReadersFilePath(roomId);
        if (!File.Exists(path))
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var loaded = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, JsonOptions);
        return loaded is null
            ? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, List<string>>(loaded, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveGroupsAsync(IEnumerable<UserGroup> groups, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(groups.ToList(), JsonOptions);
            await File.WriteAllTextAsync(GroupsFilePath, json, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<UserGroup>> LoadGroupsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(GroupsFilePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(GroupsFilePath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<UserGroup>>(json, JsonOptions) ?? [];
        }
        finally
        {
            _lock.Release();
        }
    }
}
