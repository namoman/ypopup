using Ypopup.Core.Contracts;
using Ypopup.Core.Models;

namespace Ypopup.Network.Messaging;

public sealed class GroupManager : IGroupManager
{
    private readonly IChatStorageRepository _storage;
    private readonly List<UserGroup> _groups = new();
    private readonly object _lock = new();

    public event Action? OnGroupsChanged;

    public GroupManager(IChatStorageRepository storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var stored = await _storage.LoadGroupsAsync(ct).ConfigureAwait(false);
        lock (_lock)
        {
            _groups.Clear();
            _groups.AddRange(stored);
        }
    }

    public Task<IReadOnlyList<UserGroup>> GetAllGroupsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<UserGroup> list = _groups.OrderBy(g => g.SortOrder).ToList();
            return Task.FromResult(list);
        }
    }

    public async Task<UserGroup> CreateGroupAsync(string groupName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        var newGroup = new UserGroup(
            GroupId: Guid.NewGuid().ToString(),
            GroupName: groupName,
            MemberUserIds: Array.Empty<string>(),
            SortOrder: _groups.Count
        );

        List<UserGroup> snapshot;
        lock (_lock)
        {
            _groups.Add(newGroup);
            snapshot = _groups.ToList();
        }

        await _storage.SaveGroupsAsync(snapshot, ct).ConfigureAwait(false);
        OnGroupsChanged?.Invoke();
        return newGroup;
    }

    public async Task DeleteGroupAsync(string groupId, CancellationToken ct = default)
    {
        List<UserGroup> snapshot;
        lock (_lock)
        {
            _groups.RemoveAll(g => string.Equals(g.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
            snapshot = _groups.ToList();
        }

        await _storage.SaveGroupsAsync(snapshot, ct).ConfigureAwait(false);
        OnGroupsChanged?.Invoke();
    }

    public async Task AssignUserToGroupAsync(string groupId, string userId, CancellationToken ct = default)
    {
        List<UserGroup> snapshot;
        lock (_lock)
        {
            var idx = _groups.FindIndex(g => string.Equals(g.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                var current = _groups[idx];
                if (!current.MemberUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase))
                {
                    var updatedMembers = current.MemberUserIds.Concat(new[] { userId }).ToList();
                    _groups[idx] = current with { MemberUserIds = updatedMembers };
                }
            }
            snapshot = _groups.ToList();
        }

        await _storage.SaveGroupsAsync(snapshot, ct).ConfigureAwait(false);
        OnGroupsChanged?.Invoke();
    }

    public async Task RemoveUserFromGroupAsync(string groupId, string userId, CancellationToken ct = default)
    {
        List<UserGroup> snapshot;
        lock (_lock)
        {
            var idx = _groups.FindIndex(g => string.Equals(g.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                var current = _groups[idx];
                var updatedMembers = current.MemberUserIds.Where(id => !string.Equals(id, userId, StringComparison.OrdinalIgnoreCase)).ToList();
                _groups[idx] = current with { MemberUserIds = updatedMembers };
            }
            snapshot = _groups.ToList();
        }

        await _storage.SaveGroupsAsync(snapshot, ct).ConfigureAwait(false);
        OnGroupsChanged?.Invoke();
    }

    public async Task ReorderGroupsAsync(IEnumerable<string> orderedGroupIds, CancellationToken ct = default)
    {
        var idList = orderedGroupIds.ToList();
        List<UserGroup> snapshot;
        lock (_lock)
        {
            for (var i = 0; i < idList.Count; i++)
            {
                var targetId = idList[i];
                var idx = _groups.FindIndex(g => string.Equals(g.GroupId, targetId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _groups[idx] = _groups[idx] with { SortOrder = i };
                }
            }
            snapshot = _groups.ToList();
        }

        await _storage.SaveGroupsAsync(snapshot, ct).ConfigureAwait(false);
        OnGroupsChanged?.Invoke();
    }

    public async Task SetGroupExpandedAsync(string groupId, bool isExpanded, CancellationToken ct = default)
    {
        List<UserGroup> snapshot;
        lock (_lock)
        {
            var idx = _groups.FindIndex(g => string.Equals(g.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _groups[idx] = _groups[idx] with { IsExpanded = isExpanded };
            }
            snapshot = _groups.ToList();
        }

        await _storage.SaveGroupsAsync(snapshot, ct).ConfigureAwait(false);
    }
}
