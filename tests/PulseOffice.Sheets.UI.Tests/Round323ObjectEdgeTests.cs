using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.UI.GridParts;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round323ObjectEdgeTests
{
    private readonly SheetObjectEdges _edges = new();

    [Fact]
    public void Resizing_past_the_left_and_top_edges_stops_the_dragged_edges_and_keeps_the_opposite_ones()
    {
        var clamped = _edges.Clamp(new ObjectBox(-30, -10, 130, 60), 0, 0, resizing: true);

        Assert.Equal(new ObjectBox(0, 0, 100, 50), clamped);
    }

    [Fact]
    public void Moving_past_the_edges_keeps_the_size()
    {
        var clamped = _edges.Clamp(new ObjectBox(-30, -10, 130, 60), 0, 0, resizing: false);

        Assert.Equal(new ObjectBox(0, 0, 130, 60), clamped);
    }
}
