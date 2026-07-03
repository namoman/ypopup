namespace Ypopup.Core.Sharing;

public sealed record SharedFolderHostStartResult(
    bool IsRunning,
    string? ErrorMessage = null,
    IReadOnlyList<string>? BoundPrefixes = null);
