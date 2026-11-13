using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MapleMessenger.Native;

/// <summary>The MapleStory window in front.</summary>
internal readonly record struct GameWindow(IntPtr Hwnd, int Pid, uint Thread, bool RunsAsAdmin);

internal static class Game
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int size, out int returned);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private static int _lastPid;
    private static bool _lastIsGame, _lastAdmin;

    /// <summary>Any process with "maple" in its name: MapleStory.exe and most private-server clients.</summary>
    public static bool IsGameName(string processName) => processName.Contains("maple", StringComparison.OrdinalIgnoreCase);

    /// <returns>The game window, when the window in front belongs to the game; otherwise null.</returns>
    public static GameWindow? Foreground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        uint thread = GetWindowThreadProcessId(hwnd, out int pid);
        if (pid == 0 || pid == Environment.ProcessId) return null; // our own window has "Maple" in its name too
        if (pid != _lastPid)
        {
            _lastPid = pid;
            _lastIsGame = NameOf(pid) is { } name && IsGameName(name);
            _lastAdmin = _lastIsGame && RunsAsAdmin(pid);
        }
        return _lastIsGame ? new GameWindow(hwnd, pid, thread, _lastAdmin) : null;
    }

    public static bool StillForeground(IntPtr hwnd) => GetForegroundWindow() == hwnd;

    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr param);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }

    private static readonly Dictionary<int, (bool IsGame, bool Admin)> Known = [];

    /// <returns>The game's main window wherever it is (in front, behind, on another screen), or null when the game isn't open.</returns>
    public static GameWindow? Find()
    {
        // the one in front wins, so with two clients open it types into the one last used
        if (Foreground() is { } front) return front;
        GameWindow? best = null;
        long bestArea = 0;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4 /* owner */) != IntPtr.Zero) return true;
            uint thread = GetWindowThreadProcessId(hwnd, out int pid);
            if (pid == 0 || pid == Environment.ProcessId) return true;
            if (!Known.TryGetValue(pid, out var k))
            {
                bool game = NameOf(pid) is { } name && IsGameName(name);
                Known[pid] = k = (game, game && RunsAsAdmin(pid));
            }
            if (!k.IsGame || !GetWindowRect(hwnd, out var r)) return true;
            long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
            if (area > bestArea) { bestArea = area; best = new GameWindow(hwnd, pid, thread, k.Admin); }
            return true;
        }, IntPtr.Zero);
        if (Known.Count > 200) Known.Clear(); // process ids get reused; start over now and then
        return best;
    }

    public static bool Alive(IntPtr hwnd) => IsWindow(hwnd);

    private static string? NameOf(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.ProcessName; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception) { return null; }
    }

    /// <summary>A program running as administrator only takes keys from another administrator program (a Windows rule).</summary>
    private static bool RunsAsAdmin(int pid)
    {
        var process = OpenProcess(0x1000 /* query limited information */, false, pid);
        if (process == IntPtr.Zero) return true; // we can't even look at it, so it is above us
        try
        {
            if (!OpenProcessToken(process, 0x8 /* query */, out var token)) return true;
            try { return GetTokenInformation(token, 20 /* TokenElevation */, out int elevated, 4, out _) && elevated != 0; }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(process); }
    }
}
