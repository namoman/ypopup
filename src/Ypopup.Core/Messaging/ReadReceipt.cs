namespace Ypopup.Core.Messaging;

public static class ReadReceipt
{
    public static string WriteMessageIds(IEnumerable<string> messageIds)
    {
        return string.Join(
            '\n',
            messageIds.Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> ReadMessageIds(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        return body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int CountOthers(IEnumerable<string>? readerIds, string senderId)
    {
        if (readerIds is null)
        {
            return 0;
        }

        return readerIds.Count(id => !string.Equals(id, senderId, StringComparison.OrdinalIgnoreCase));
    }
}
