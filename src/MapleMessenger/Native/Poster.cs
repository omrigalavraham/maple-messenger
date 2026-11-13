using System.Runtime.InteropServices;

namespace MapleMessenger.Native;

/// <summary>
/// Types into the game's window directly, through its message queue, instead of through the keyboard.
/// The game doesn't have to be in front, and the player's own keyboard and its language don't matter.
/// </summary>
internal static class Poster
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WmKeyDown = 0x100, WmKeyUp = 0x101, WmChar = 0x102;
    private const int VkReturn = 0x0D, EnterScan = 0x1C;

    /// <summary>The pause after each letter, and after Enter (the chat box opening or sending takes the game a frame or two).</summary>
    public const int CharMs = 12, EnterMs = 60;

    /// <returns>True when the whole line went through.</returns>
    public static bool Type(IntPtr hwnd, string text, bool enterFirst, Func<bool> stillOk, CancellationToken ct)
    {
        if (enterFirst && !Enter(hwnd)) return false;
        foreach (char ch in text)
        {
            if (ct.IsCancellationRequested || !stillOk()) return false;
            if (!PostMessageW(hwnd, WmChar, ch, 1)) return false;
            Thread.Sleep(CharMs);
        }
        if (ct.IsCancellationRequested || !stillOk()) return false;
        return Enter(hwnd);
    }

    /// <summary>Enter goes as a key, the way the game's own message loop turns it into a character if it wants one.</summary>
    private static bool Enter(IntPtr hwnd)
    {
        const int scan = EnterScan << 16;
        bool ok = PostMessageW(hwnd, WmKeyDown, VkReturn, 1 | scan)
                  && PostMessageW(hwnd, WmKeyUp, VkReturn, unchecked((int)(1 | scan | 0xC0000000)));
        Thread.Sleep(EnterMs);
        return ok;
    }
}
