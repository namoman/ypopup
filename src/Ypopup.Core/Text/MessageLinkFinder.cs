using System.Text.RegularExpressions;

namespace Ypopup.Core.Text;

public readonly record struct MessageLink(int Start, int Length, string Target);

public readonly record struct MessageTextPart(string Text, string? LinkTarget);

public static class MessageLinkFinder
{
    private const string UrlChars = @"[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;=%]";

    private static readonly Regex LinkPattern = new(
        $@"(?i)(?:(?:https?|file|smb)://{UrlChars}+|www\.{UrlChars}+|\\\\[^\s\\]+(?:\\[^\s]*)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    public static IReadOnlyList<MessageLink> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        Match match;
        try
        {
            match = LinkPattern.Match(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }

        var links = new List<MessageLink>();
        while (match.Success)
        {
            var trimmed = TrimEndPunctuation(match.Value);
            if (IsUsable(trimmed))
            {
                links.Add(new MessageLink(match.Index, trimmed.Length, trimmed));
            }

            match = match.NextMatch();
        }

        return links;
    }

    public static IReadOnlyList<MessageTextPart> Split(string? text)
    {
        var source = text ?? string.Empty;
        var links = Find(source);
        if (links.Count == 0)
        {
            return [new MessageTextPart(source, null)];
        }

        var parts = new List<MessageTextPart>(links.Count * 2 + 1);
        var index = 0;
        foreach (var link in links)
        {
            if (link.Start > index)
            {
                parts.Add(new MessageTextPart(source[index..link.Start], null));
            }

            parts.Add(new MessageTextPart(source.Substring(link.Start, link.Length), link.Target));
            index = link.Start + link.Length;
        }

        if (index < source.Length)
        {
            parts.Add(new MessageTextPart(source[index..], null));
        }

        return parts;
    }

    public static MessageLink? At(string? text, int charIndex)
    {
        foreach (var link in Find(text))
        {
            if (charIndex >= link.Start && charIndex < link.Start + link.Length)
            {
                return link;
            }
        }

        return null;
    }

    public static string? ToLaunchTarget(string target)
    {
        if (target.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + target;
        }

        if (target.StartsWith(@"\\", StringComparison.Ordinal)
            || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("smb://", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        return null;
    }

    private static bool IsUsable(string value)
    {
        if (value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return value.Length > "www.".Length;
        }

        if (value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return value.Length > 2;
        }

        return value.Contains("://", StringComparison.Ordinal) && value.Length > value.IndexOf("://", StringComparison.Ordinal) + 3;
    }

    private static string TrimEndPunctuation(string value)
    {
        var end = value.Length;
        while (end > 0 && ShouldTrim(value.AsSpan(0, end)))
        {
            end--;
        }

        return value[..end];
    }

    private static bool ShouldTrim(ReadOnlySpan<char> value)
    {
        var last = value[^1];
        if (last is ')' or '）')
        {
            var open = 0;
            var close = 0;
            foreach (var ch in value)
            {
                if (ch is '(' or '（')
                {
                    open++;
                }
                else if (ch is ')' or '）')
                {
                    close++;
                }
            }

            return close > open;
        }

        return last is '.' or ',' or ';' or ':' or '!' or '?' or '。' or '、' or '，'
            or ']' or '}' or '>' or '》' or '」' or '』' or '"' or '\'' or '”' or '’';
    }
}
