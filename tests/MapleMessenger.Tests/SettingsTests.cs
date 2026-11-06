using MapleMessenger.Core;

namespace MapleMessenger.Tests;

public class SettingsTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 3)]
    [InlineData(10, 10)]
    [InlineData(5000, 600)]
    public void Interval_is_kept_in_range(int given, int expected)
    {
        var s = new Settings { IntervalSeconds = given }.Normalize();
        Assert.Equal(expected, s.IntervalSeconds);
    }

    [Fact]
    public void Unknown_hotkey_falls_back_to_F9()
    {
        Assert.Equal("F9", new Settings { Hotkey = "Q" }.Normalize().Hotkey);
        Assert.Equal("F11", new Settings { Hotkey = "F11" }.Normalize().Hotkey);
    }

    [Fact]
    public void There_are_always_two_messages()
    {
        Assert.Equal(["a", ""], new Settings { Messages = ["a"] }.Normalize().Messages);
        Assert.Equal(["", ""], new Settings { Messages = null! }.Normalize().Messages);
        Assert.Equal(["a", "b"], new Settings { Messages = ["a", "b", "c"] }.Normalize().Messages);
    }

    [Fact]
    public void Hotkeys_map_to_virtual_keys()
    {
        Assert.Equal(0x78, Settings.HotkeyVk("F9"));
        Assert.Equal(0x13, Settings.HotkeyVk("Pause"));
    }

    [Fact]
    public void Save_and_load_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), "mm-test-" + Guid.NewGuid() + ".json");
        try
        {
            new Settings { Messages = ["S> 10 apples", "B> scroll"], IntervalSeconds = 25, Hotkey = "F10", EnterFirst = false }.Save(path);
            var s = Settings.Load(path);
            Assert.Equal(["S> 10 apples", "B> scroll"], s.Messages);
            Assert.Equal(25, s.IntervalSeconds);
            Assert.Equal("F10", s.Hotkey);
            Assert.False(s.EnterFirst);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_missing_or_broken_file_gives_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "mm-test-" + Guid.NewGuid() + ".json");
        Assert.Equal(10, Settings.Load(path).IntervalSeconds);
        File.WriteAllText(path, "{ not json");
        try { Assert.Equal("F9", Settings.Load(path).Hotkey); }
        finally { File.Delete(path); }
    }
}
