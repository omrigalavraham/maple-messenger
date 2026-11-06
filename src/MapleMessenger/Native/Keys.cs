using System.Runtime.InteropServices;
using MapleMessenger.Core;

namespace MapleMessenger.Native;

/// <summary>Which physical key types a character, in a given keyboard language.</summary>
internal static class Keys
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScanExW(char ch, IntPtr hkl);
    [DllImport("user32.dll")] private static extern uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl);
    [DllImport("user32.dll")] private static extern int GetKeyboardLayoutList(int count, IntPtr[]? list);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")] private static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);

    private const ushort SpaceScan = 0x39;
    private static readonly int[] Modifiers = [0x10, 0x11, 0x12, 0x5B, 0x5C]; // Shift, Ctrl, Alt, both Windows keys

    public static IntPtr[] Layouts()
    {
        int n = GetKeyboardLayoutList(0, null);
        var list = new IntPtr[n];
        GetKeyboardLayoutList(n, list);
        return list;
    }

    /// <summary>The keyboard language active in a window's thread (the game's, when typing into it).</summary>
    public static IntPtr LayoutOf(uint threadId) => GetKeyboardLayout(threadId);

    public static bool CapsLock => (GetKeyState(0x14) & 1) != 0;

    /// <summary>Shift, Ctrl, Alt or Windows held right now: letters typed now would become shortcuts.</summary>
    public static bool ModifierDown => Modifiers.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);

    /// <returns>The key and Shift state for <paramref name="ch"/>, or null when this keyboard language can't type it.</returns>
    public static KeyOf? Map(char ch, IntPtr hkl, bool capsLock)
    {
        if (ch == ' ') return new KeyOf(SpaceScan, false);
        short r = VkKeyScanExW(ch, hkl);
        if (r == -1) return null;
        int vk = r & 0xFF, mods = (r >> 8) & 0xFF;
        if ((mods & ~1) != 0) return null; // needs Ctrl or Alt (AltGr): not a plain typing key
        uint scan = MapVirtualKeyExW((uint)vk, 0 /* virtual key to scan code */, hkl);
        if (scan == 0) return null;
        bool shift = (mods & 1) != 0;
        if (capsLock && char.IsLetter(ch)) shift = !shift; // Caps Lock flips letters, so Shift flips them back
        return new KeyOf((ushort)scan, shift);
    }
}
