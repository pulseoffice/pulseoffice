using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round181ChartGeometryTests
{
    private static readonly Rect Plot = new(0, 0, 300, 200);

    private static readonly ChartData Data = new(["A", "B"], [new ChartSeries("Small", [1, 2]), new ChartSeries("Large", [100, 200])]);

    private static ChartDefinition Definition(ChartType type) =>
        new ChartDefinition { Id = "c", Type = type, SourceSheet = "S", SourceRange = "A1:C3" }
            .WithSeriesFormat(1, new ChartSeriesFormat { SecondaryAxis = true });

    private static ChartFrame Frame(ChartDefinition definition) => new(null!, definition, Data, Plot, Plot);

    [Theory]
    [InlineData(ChartType.StackedColumn)]
    [InlineData(ChartType.StackedBar)]
    [InlineData(ChartType.Bar)]
    public void Secondary_series_get_their_own_scale_on_stacked_and_horizontal_bars(ChartType type)
    {
        var plot = new BarPlot();
        var frame = Frame(Definition(type));

        var primary = plot.ValueAxis(frame)!;
        var secondary = plot.SecondaryValueAxis(frame);

        Assert.NotNull(secondary);
        Assert.True(primary.Maximum < 10);
        Assert.True(secondary.Maximum >= 200);
        Assert.Equal(secondary.Maximum, plot.SeriesScale(frame, 1)!.Maximum);
    }

    [Fact]
    public void Stacked_secondary_series_stand_beside_the_primary_stack()
    {
        var plot = new BarPlot();
        var frame = Frame(Definition(ChartType.StackedColumn));
        var labels = plot.DataLabels(frame).Where(label => label.Index == 0).ToList();

        var small = labels.Single(label => label.Series == 0);
        var large = labels.Single(label => label.Series == 1);

        Assert.True(large.Anchor.X > small.Anchor.X);
        Assert.Equal(plot.SecondaryValueAxis(frame)!.Position(100), large.Anchor.Y, 3);
    }

    [Fact]
    public void Columns_grow_from_the_value_where_the_category_axis_crosses()
    {
        var marks = new ChartAxisMarks();
        var scale = new PlotScale(0, 10, 200, 0);

        Assert.Equal(0, marks.ValueCrossing(scale, ChartAxisScale.Automatic));
        Assert.Equal(10, marks.ValueCrossing(scale, new ChartAxisScale { Crossing = AxisCrossing.Maximum }));
        Assert.Equal(4, marks.ValueCrossing(scale, new ChartAxisScale { Crossing = AxisCrossing.Value, CrossingValue = 4 }));
        Assert.Equal(10, marks.ValueCrossing(scale, new ChartAxisScale { Crossing = AxisCrossing.Value, CrossingValue = 40 }));
    }

    [Fact]
    public void The_value_axis_crosses_at_a_category_boundary()
    {
        var marks = new ChartAxisMarks();
        var categories = new CategoryScale(4, 0, 400);

        Assert.Equal(0, categories.Boundary(marks.CategoryCrossing(4, ChartAxisScale.Automatic)));
        Assert.Equal(400, categories.Boundary(marks.CategoryCrossing(4, new ChartAxisScale { Crossing = AxisCrossing.Maximum })));
        Assert.Equal(200, categories.Boundary(marks.CategoryCrossing(4, new ChartAxisScale { Crossing = AxisCrossing.Value, CrossingValue = 3 })));
    }

    [Fact]
    public void Minor_ticks_fall_between_major_divisions()
    {
        var marks = new ChartAxisMarks();
        var scale = new PlotScale(0, 10, 200, 0) { MajorStep = 5 };

        var ticks = marks.MinorTicks(scale, new ChartAxisScale { MajorUnit = 5, MinorUnit = 1 });

        Assert.Equal(new double[] { 1, 2, 3, 4, 6, 7, 8, 9 }, ticks);
        Assert.Empty(marks.MinorTicks(scale, ChartAxisScale.Automatic));
    }
}
