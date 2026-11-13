using MapleMessenger.Core;

namespace MapleMessenger.Native;

internal static class Typer
{
    /// <summary>How long a key stays down, and the pause after it comes up. Games read keys per frame, so not too fast.</summary>
    public const int HoldMs = 12, GapMs = 14;

    /// <summary>Presses the keys one by one. Stops the moment <paramref name="stillOk"/> fails (the game lost focus)
    /// or on cancel, and never leaves a key held down.</summary>
    /// <returns>True when every key was pressed.</returns>
    public static bool Type(IReadOnlyList<Stroke> strokes, Func<bool> stillOk, CancellationToken ct)
    {
        var down = new HashSet<ushort>();
        try
        {
            foreach (var s in strokes)
            {
                if (ct.IsCancellationRequested || !stillOk()) return false;
                if (!Input.Send(s)) return false;
                if (s.Up) down.Remove(s.Scan); else down.Add(s.Scan);
                Thread.Sleep(s.Up ? GapMs : HoldMs);
            }
            return true;
        }
        finally
        {
            foreach (var scan in down) Input.Send(new Stroke(scan, Up: true));
        }
    }
}
