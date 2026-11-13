using MapleMessenger.Core;

namespace MapleMessenger.Tests;

public class SettingsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
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

public class PacingTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Interval_counts_from_the_start_of_the_message()
    {
        // 4 seconds apart, typing took 3: one second to wait
        Assert.Equal(T0.AddSeconds(4), Settings.NextStart(T0, T0.AddSeconds(3), 4));
    }

    [Fact]
    public void Typing_longer_than_the_interval_still_leaves_a_short_breath()
    {
        Assert.Equal(T0.AddSeconds(3).AddMilliseconds(Settings.MinGapMs), Settings.NextStart(T0, T0.AddSeconds(3), 1));
    }

    [Fact]
    public void Background_typing_is_on_by_default()
    {
        Assert.True(new Settings().Normalize().Background);
    }
}
