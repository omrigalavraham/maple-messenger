using System.Runtime.InteropServices;
using MapleMessenger.Core;

namespace MapleMessenger.Native;

/// <summary>Presses keys by scan code, the way a physical keyboard reports them.</summary>
internal static class Input
{
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint Type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse; // the largest member: it sets the union's size
        [FieldOffset(0)] public KEYBDINPUT Key;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int Dx, Dy; public uint Data, Flags, Time; public IntPtr Extra; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    private const uint Keyboard = 1, KeyUp = 0x2, ScanCode = 0x8;

    public static int InputSize => Marshal.SizeOf<INPUT>();

    public static bool Send(Stroke s)
    {
        var input = new INPUT
        {
            Type = Keyboard,
            U = new InputUnion { Key = new KEYBDINPUT { Scan = s.Scan, Flags = ScanCode | (s.Up ? KeyUp : 0) } },
        };
        return SendInput(1, [input], InputSize) == 1;
    }
}
