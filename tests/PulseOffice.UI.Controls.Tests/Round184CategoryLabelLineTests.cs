using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round184CategoryLabelLineTests
{
    private readonly ChartAxisMarks _marks = new();

    [Fact]
    public void Category_labels_stay_at_the_edge_when_the_crossing_is_automatic()
    {
        var values = new PlotScale(0, 10, 200, 0);

        Assert.Equal(200, _marks.CategoryAxisLine(values, ChartAxisScale.Automatic, 200));
    }

    [Fact]
    public void Category_labels_follow_the_axis_line_on_vertical_charts()
    {
        var values = new PlotScale(0, 10, 200, 0);

        Assert.Equal(100, _marks.CategoryAxisLine(values, new ChartAxisScale { Crossing = AxisCrossing.Value, CrossingValue = 5 }, 200));
        Assert.Equal(0, _marks.CategoryAxisLine(values, new ChartAxisScale { Crossing = AxisCrossing.Maximum }, 200));
    }

    [Fact]
    public void Category_labels_follow_the_axis_line_on_horizontal_charts()
    {
        var values = new PlotScale(0, 10, 0, 300);

        Assert.Equal(300, _marks.CategoryAxisLine(values, new ChartAxisScale { Crossing = AxisCrossing.Maximum }, 0));
    }
}
