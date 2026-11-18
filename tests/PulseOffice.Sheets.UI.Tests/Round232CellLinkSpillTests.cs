using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.UI;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round232CellLinkSpillTests
{
    private readonly ICellLinkHitArea _area = new CellLinkHitArea();

    [Fact]
    public void Text_overflowing_to_the_right_is_clickable_in_the_neighbour()
    {
        var span = _area.Spilled(0, 50, new OverflowSpan(0, 200), 120, 3, CellHorizontalAlignment.Left);
        Assert.True(span.Contains(110));
        Assert.False(span.Contains(130));
    }

    [Fact]
    public void Text_overflowing_to_the_left_stops_at_the_spill()
    {
        var span = _area.Spilled(100, 150, new OverflowSpan(60, 150), 120, 3, CellHorizontalAlignment.Right);
        Assert.Equal(60, span.Left);
        Assert.Equal(147, span.Right);
    }
}
