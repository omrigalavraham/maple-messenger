using MapleMessenger.Core;

namespace MapleMessenger.Tests;

public class RotationTests
{
    [Fact]
    public void Two_messages_take_turns()
    {
        string[] m = ["sell", "buy"];
        int i = Rotation.First(m);
        Assert.Equal(0, i);
        i = Rotation.Next(m, i);
        Assert.Equal(1, i);
        i = Rotation.Next(m, i);
        Assert.Equal(0, i);
    }

    [Fact]
    public void An_empty_message_is_skipped()
    {
        string[] m = ["  ", "buy"];
        Assert.Equal(1, Rotation.First(m));
        Assert.Equal(1, Rotation.Next(m, 1));
    }

    [Fact]
    public void Nothing_to_send_gives_minus_one()
    {
        string[] m = ["", " \n "];
        Assert.Equal(-1, Rotation.First(m));
        Assert.Equal(-1, Rotation.Next(m, 0));
    }

    [Fact]
    public void Clean_turns_line_breaks_into_spaces_and_trims()
    {
        Assert.Equal("S> sword B> shield", Rotation.Clean("  S> sword\r\nB> shield \n"));
    }
}
