using Avalonia.Input;
using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round278KioskKeysTests
{
    [Theory]
    [InlineData(Key.Right, false)]
    [InlineData(Key.Space, false)]
    [InlineData(Key.B, false)]
    [InlineData(Key.D3, false)]
    [InlineData(Key.Escape, true)]
    public void KioskAcceptsOnlyEscape(Key key, bool allowed)
    {
        Assert.Equal(allowed, new ShowLoopClock(kiosk: true, 0, 0).AllowsKey(key));
        Assert.True(new ShowLoopClock(kiosk: false, 0, 0).AllowsKey(key));
    }

    [Fact]
    public void RestartNeedsAKioskAndADelay()
    {
        Assert.True(new ShowLoopClock(kiosk: true, 0, 1000).Restarts);
        Assert.False(new ShowLoopClock(kiosk: true, 0, 0).Restarts);
        Assert.False(new ShowLoopClock(kiosk: false, 0, 1000).Restarts);
    }
}
