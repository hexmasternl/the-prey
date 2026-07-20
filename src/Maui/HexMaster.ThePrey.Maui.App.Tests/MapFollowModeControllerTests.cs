using HexMaster.ThePrey.Maui.App.Services.Navigation;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class MapFollowModeControllerTests
{
    [Fact]
    public void IsFollowing_ShouldDefaultToTrue()
    {
        // Matches the HUD's Center toggle, which starts on — a fresh game opens pinned to the player.
        Assert.True(new MapFollowModeController().IsFollowing);
    }

    [Fact]
    public void SetFollowMode_ShouldExposeTheNewValue_AndRaiseIt()
    {
        var sut = new MapFollowModeController();
        var raised = new List<bool>();
        sut.FollowModeChanged += (_, following) => raised.Add(following);

        sut.SetFollowMode(false);
        sut.SetFollowMode(true);

        Assert.True(sut.IsFollowing);
        Assert.Equal([false, true], raised);
    }

    [Fact]
    public void SetFollowMode_ShouldRaise_EvenWhenTheValueIsUnchanged()
    {
        // The HUD re-asserts the current mode on activation. A map page that subscribed after the last
        // toggle relies on that repeat to centre itself, so this must not be suppressed as a no-op.
        var sut = new MapFollowModeController();
        var raised = 0;
        sut.FollowModeChanged += (_, _) => raised++;

        sut.SetFollowMode(true);
        sut.SetFollowMode(true);

        Assert.Equal(2, raised);
    }
}
