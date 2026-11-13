namespace MapleMessenger.Tests;

public class GateTests
{
    [Fact]
    public void Nothing_to_send_comes_first()
    {
        Assert.Equal(Phase.NoText, Gate.Blocker(hasText: false, gameFound: true, gameAdmin: false, weAdmin: false));
    }

    [Fact]
    public void Never_types_unless_the_game_is_in_front()
    {
        Assert.Equal(Phase.WaitingForGame, Gate.Blocker(hasText: true, gameFound: false, gameAdmin: false, weAdmin: true));
    }

    [Fact]
    public void A_game_running_as_admin_needs_us_as_admin()
    {
        Assert.Equal(Phase.NeedsAdmin, Gate.Blocker(hasText: true, gameFound: true, gameAdmin: true, weAdmin: false));
        Assert.Null(Gate.Blocker(hasText: true, gameFound: true, gameAdmin: true, weAdmin: true));
    }

    [Fact]
    public void Ready_when_nothing_blocks()
    {
        Assert.Null(Gate.Blocker(hasText: true, gameFound: true, gameAdmin: false, weAdmin: false));
    }
}
