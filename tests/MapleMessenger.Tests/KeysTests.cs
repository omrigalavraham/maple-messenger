using MapleMessenger.Core;
using MapleMessenger.Native;

namespace MapleMessenger.Tests;

public class KeysTests
{
    private static IntPtr Layout(int langId) =>
        Keys.Layouts().FirstOrDefault(h => ((long)h & 0xFFFF) == langId);

    [Fact]
    public void English_layout_gives_real_scan_codes()
    {
        var en = Layout(0x0409);
        if (en == IntPtr.Zero) return; // no US English layout on this machine
        Assert.Equal(new KeyOf(0x1E, false), Keys.Map('a', en, capsLock: false));
        Assert.Equal(new KeyOf(0x1E, true), Keys.Map('A', en, capsLock: false));
        Assert.Equal(new KeyOf(0x02, true), Keys.Map('!', en, capsLock: false));
        Assert.Equal(new KeyOf(0x39, false), Keys.Map(' ', en, capsLock: false));
        Assert.Equal(new KeyOf(0x0B, false), Keys.Map('0', en, capsLock: false));
    }

    [Fact]
    public void Caps_lock_flips_shift_for_letters_only()
    {
        var en = Layout(0x0409);
        if (en == IntPtr.Zero) return;
        Assert.Equal(new KeyOf(0x1E, true), Keys.Map('a', en, capsLock: true));
        Assert.Equal(new KeyOf(0x1E, false), Keys.Map('A', en, capsLock: true));
        Assert.Equal(new KeyOf(0x02, true), Keys.Map('!', en, capsLock: true));
    }

    [Fact]
    public void Hebrew_layout_cannot_type_english_letters()
    {
        var he = Layout(0x040D);
        if (he == IntPtr.Zero) return; // no Hebrew layout on this machine
        Assert.Null(Keys.Map('a', he, capsLock: false));
    }

    [Fact]
    public void Input_struct_has_the_size_windows_expects()
    {
        Assert.Equal(Environment.Is64BitProcess ? 40 : 28, Input.InputSize);
    }
}
