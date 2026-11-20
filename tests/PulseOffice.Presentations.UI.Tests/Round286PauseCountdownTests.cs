using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round286PauseCountdownTests
{
    [Fact]
    public void ThePauseCountsDownWholeSecondsToZero()
    {
        var clock = new ShowLoopClock(kiosk: false, 3000, 0);

        Assert.Equal(3, clock.SecondsLeft(0));
        Assert.Equal(2, clock.SecondsLeft(1000));
        Assert.Equal(1, clock.SecondsLeft(2999));
        Assert.Equal(0, clock.SecondsLeft(5000));
    }
}
