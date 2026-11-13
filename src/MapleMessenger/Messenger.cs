using MapleMessenger.Core;
using MapleMessenger.Native;

namespace MapleMessenger;

internal enum Phase { Off, NoText, WaitingForGame, NeedsAdmin, WrongLanguage, Typing, Countdown }

internal static class Gate
{
    /// <summary>What stops the next message right now, or null when it can be typed.</summary>
    /// <param name="gameFound">The game is in front, or (when typing in the background) open at all.</param>
    public static Phase? Blocker(bool hasText, bool gameFound, bool gameAdmin, bool weAdmin) =>
        !hasText ? Phase.NoText
        : !gameFound ? Phase.WaitingForGame
        : gameAdmin && !weAdmin ? Phase.NeedsAdmin
        : null;
}

/// <summary>
/// Types the messages in turn into the game. Runs on the window's thread; only the key presses
/// themselves happen in the background. Types only while the game is the window in front.
/// </summary>
internal sealed class Messenger(Func<Settings> settings)
{
    /// <summary>A pause before typing, after starting or after switching into the game, so the keys used for that are let go.</summary>
    private const int SettleMs = 400;

    private CancellationTokenSource? _cts;

    public Phase Phase { get; private set; } = Phase.Off;
    /// <summary>The message being typed, or the next one in line.</summary>
    public int Slot { get; private set; } = -1;
    public string Missing { get; private set; } = "";
    public int Sent { get; private set; }
    public DateTime CountdownStart { get; private set; }
    public DateTime CountdownEnd { get; private set; }
    public bool Running => _cts != null;
    public event Action? Changed;

    public double Progress => Phase == Phase.Countdown && CountdownEnd > CountdownStart
        ? Math.Clamp((DateTime.UtcNow - CountdownStart) / (CountdownEnd - CountdownStart), 0, 1)
        : 0;

    public int SecondsLeft => (int)Math.Ceiling(Math.Max(0, (CountdownEnd - DateTime.UtcNow).TotalSeconds));

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _ = Run(_cts.Token);
    }

    public void Stop()
    {
        if (_cts == null) return;
        _cts.Cancel();
        _cts = null;
        Set(Phase.Off);
    }

    private async Task Run(CancellationToken ct)
    {
        int slot = -1;
        bool settle = true;
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var s = settings();
                if (slot < 0 || Rotation.Clean(s.Messages[slot]).Length == 0) slot = Rotation.Next(s.Messages, slot);
                Slot = slot;

                var game = s.Background ? Game.Find() : Game.Foreground();
                if (Gate.Blocker(slot >= 0, game is not null, game?.RunsAsAdmin == true, Environment.IsPrivilegedProcess) is { } block)
                {
                    settle = true;
                    Set(block);
                    await Task.Delay(150, ct);
                    continue;
                }
                var g = game!.Value;
                var text = Rotation.Clean(s.Messages[slot]);
                if (s.Background)
                {
                    // straight into the game's window: the keyboard, its language and what's in front don't matter
                    Set(Phase.Typing);
                    var started = DateTime.UtcNow;
                    bool posted = await Task.Run(() => Poster.Type(g.Hwnd, text, s.EnterFirst, () => Game.Alive(g.Hwnd), ct), CancellationToken.None);
                    ct.ThrowIfCancellationRequested();
                    if (!posted) { await Task.Delay(150, ct); continue; } // the game closed: the same message goes again
                    await CountAndWait(s, started, ct);
                    slot = Rotation.Next(s.Messages, slot);
                    continue;
                }
                if (settle)
                {
                    settle = false;
                    Set(Phase.Typing);
                    await Task.Delay(SettleMs, ct);
                    continue; // look again: the player may have left the game meanwhile
                }
                if (Keys.ModifierDown) { await Task.Delay(50, ct); continue; } // letters now would be shortcuts

                var hkl = Keys.LayoutOf(g.Thread);
                bool caps = Keys.CapsLock;
                var plan = KeyPlan.Build(text, c => Keys.Map(c, hkl, caps), s.EnterFirst);
                if (plan.Strokes is not { } strokes)
                {
                    Missing = plan.Missing;
                    settle = true;
                    Set(Phase.WrongLanguage, force: true);
                    await Task.Delay(250, ct);
                    continue;
                }

                Set(Phase.Typing);
                var start = DateTime.UtcNow;
                bool done = await Task.Run(() => Typer.Type(strokes, () => Game.StillForeground(g.Hwnd), ct), CancellationToken.None);
                ct.ThrowIfCancellationRequested();
                if (!done) { settle = true; continue; } // left the game mid-line: the same message goes again

                await CountAndWait(s, start, ct);
                slot = Rotation.Next(s.Messages, slot);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Counts the message and waits until the next one is due.</summary>
    private async Task CountAndWait(Settings s, DateTime started, CancellationToken ct)
    {
        Sent++;
        Slot = Rotation.Next(s.Messages, Slot);
        CountdownStart = DateTime.UtcNow;
        CountdownEnd = Settings.NextStart(started, CountdownStart, s.IntervalSeconds);
        Set(Phase.Countdown, force: true);
        while (DateTime.UtcNow < CountdownEnd) await Task.Delay(50, ct);
    }

    private void Set(Phase p, bool force = false)
    {
        if (p == Phase && !force) return;
        Phase = p;
        Changed?.Invoke();
    }
}
