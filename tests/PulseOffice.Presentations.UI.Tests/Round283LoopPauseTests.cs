using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round283LoopPauseTests
{
    [Fact]
    public void WithoutAPauseTheShowRestartsAtOnceAndShowsNoPauseScreen()
    {
        var clock = new ShowLoopClock(kiosk: false, 0, 0);
        var paused = 0;
        var restarted = 0;

        clock.Loop(() => paused++, () => restarted++);

        Assert.Equal(0, paused);
        Assert.Equal(1, restarted);
        Assert.False(clock.SkipPause());
    }
}
