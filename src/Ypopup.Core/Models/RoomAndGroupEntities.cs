namespace Ypopup.Core.Models;

public enum RoomType
{
    Direct1on1 = 0,
    GroupChat = 1
}

public sealed record UserGroup(
    string GroupId,
    string GroupName,
    IReadOnlyList<string>? MemberUserIds = null,
    int SortOrder = 0,
    bool IsExpanded = true
)
{
    public IReadOnlyList<string> MemberUserIds { get; init; } = MemberUserIds ?? Array.Empty<string>();
}

public sealed record ChatRoom(
    string RoomId,
    string Title,
    RoomType Type,
    IReadOnlyList<string>? ParticipantIds = null,
    DateTime CreatedAtUtc = default,
    DateTime LastMessageAtUtc = default
)
{
    public IReadOnlyList<string> ParticipantIds { get; init; } = ParticipantIds ?? Array.Empty<string>();
    public DateTime CreatedAtUtc { get; init; } = CreatedAtUtc == default ? DateTime.UtcNow : CreatedAtUtc;
    public DateTime LastMessageAtUtc { get; init; } = LastMessageAtUtc == default ? DateTime.UtcNow : LastMessageAtUtc;
}
