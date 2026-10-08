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
    }

    private string RoomsFilePath => Path.Combine(_basePath, "rooms.json");
    private string GroupsFilePath => Path.Combine(_basePath, "groups.json");
    private string GetMessageFilePath(string roomId) => Path.Combine(_basePath, "messages", $"{roomId}.jsonl");

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
