using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round314TabDropOffsetTests
{
    [Theory]
    [InlineData(40, 0, 40)]
    [InlineData(40, 300, 340)]
    [InlineData(0, 120.5, 120.5)]
    public void A_drop_on_a_scrolled_strip_is_measured_in_the_strip_content(double viewportX, double offset, double expected) =>
        Assert.Equal(expected, new TabStripDragGeometry().ContentX(viewportX, offset));
}
