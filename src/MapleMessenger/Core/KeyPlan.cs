namespace MapleMessenger.Core;

/// <summary>One physical key going down or up, by keyboard scan code.</summary>
internal readonly record struct Stroke(ushort Scan, bool Up);

/// <summary>The key that produces a character, and whether Shift must be held for it.</summary>
internal readonly record struct KeyOf(ushort Scan, bool Shift);

internal sealed record PlanResult(IReadOnlyList<Stroke>? Strokes, string Missing);

/// <summary>Turns a chat line into the exact key presses a person would make.</summary>
internal static class KeyPlan
{
    public const ushort Enter = 0x1C;
    public const ushort Shift = 0x2A;

    /// <param name="map">The character → key lookup for the game's current keyboard language.</param>
    /// <param name="enterFirst">Press Enter first to open the chat box.</param>
    /// <returns>The strokes, or null with the characters the keyboard language can't type.</returns>
    public static PlanResult Build(string text, Func<char, KeyOf?> map, bool enterFirst)
    {
        var strokes = new List<Stroke>();
        var missing = new List<char>();
        if (enterFirst) Tap(strokes, Enter);

        bool shifted = false;
        foreach (char ch in text)
        {
            if (map(ch) is not { } key)
            {
                if (!missing.Contains(ch)) missing.Add(ch);
                continue;
            }
            // like a person: hold Shift through a run of capitals, let go before a lowercase letter
            if (key.Shift != shifted)
            {
                strokes.Add(new Stroke(Shift, Up: !key.Shift));
                shifted = key.Shift;
            }
            Tap(strokes, key.Scan);
        }
        if (shifted) strokes.Add(new Stroke(Shift, Up: true));
        Tap(strokes, Enter);

        return missing.Count > 0 ? new PlanResult(null, new string(missing.ToArray())) : new PlanResult(strokes, "");
    }

    private static void Tap(List<Stroke> s, ushort scan)
    {
        s.Add(new Stroke(scan, Up: false));
        s.Add(new Stroke(scan, Up: true));
    }
}
