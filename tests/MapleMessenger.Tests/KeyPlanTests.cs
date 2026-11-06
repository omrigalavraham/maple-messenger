using MapleMessenger.Core;

namespace MapleMessenger.Tests;

public class KeyPlanTests
{
    // a fake layout: a-z map to 0x10.., capitals need Shift, space is 0x39, anything else is untypeable
    private static KeyOf? Fake(char c) =>
        c == ' ' ? new KeyOf(0x39, false)
        : c is >= 'a' and <= 'z' ? new KeyOf((ushort)(0x10 + c - 'a'), false)
        : c is >= 'A' and <= 'Z' ? new KeyOf((ushort)(0x10 + c - 'A'), true)
        : null;

    private static string Show(IEnumerable<Stroke> s) =>
        string.Join(" ", s.Select(k => (k.Up ? "^" : "v") + k.Scan.ToString("X2")));

    [Fact]
    public void Opens_chat_types_and_sends()
    {
        var r = KeyPlan.Build("ab", Fake, enterFirst: true);
        Assert.Equal("v1C ^1C v10 ^10 v11 ^11 v1C ^1C", Show(r.Strokes!));
    }

    [Fact]
    public void Without_enter_first_it_only_types_and_sends()
    {
        var r = KeyPlan.Build("a", Fake, enterFirst: false);
        Assert.Equal("v10 ^10 v1C ^1C", Show(r.Strokes!));
    }

    [Fact]
    public void Shift_is_held_across_capitals_and_released_before_lowercase()
    {
        var r = KeyPlan.Build("ABc", Fake, enterFirst: false);
        Assert.Equal("v2A v10 ^10 v11 ^11 ^2A v12 ^12 v1C ^1C", Show(r.Strokes!));
    }

    [Fact]
    public void Shift_is_released_before_the_final_enter()
    {
        var r = KeyPlan.Build("aB", Fake, enterFirst: false);
        Assert.Equal("v10 ^10 v2A v11 ^11 ^2A v1C ^1C", Show(r.Strokes!));
    }

    [Fact]
    public void Untypeable_characters_are_reported_once_and_nothing_is_typed()
    {
        var r = KeyPlan.Build("a!b!?", Fake, enterFirst: true);
        Assert.Null(r.Strokes);
        Assert.Equal("!?", r.Missing);
    }
}
