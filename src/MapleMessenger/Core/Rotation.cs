namespace MapleMessenger.Core;

/// <summary>Which message goes next: the messages take turns, and empty ones are skipped.</summary>
internal static class Rotation
{
    public static int First(IReadOnlyList<string> messages) => Next(messages, -1);

    /// <returns>The next message with text after <paramref name="from"/>, wrapping around; -1 when all are empty.</returns>
    public static int Next(IReadOnlyList<string> messages, int from)
    {
        int n = messages.Count;
        for (int step = 1; step <= n; step++)
        {
            int i = ((from + step) % n + n) % n;
            if (Clean(messages[i]).Length > 0) return i;
        }
        return -1;
    }

    /// <summary>A chat line is one line: line breaks become spaces, and the ends are trimmed.</summary>
    public static string Clean(string? text) =>
        (text ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
}
