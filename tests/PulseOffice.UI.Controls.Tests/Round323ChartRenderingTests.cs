using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round323ChartRenderingTests
{
    private static readonly Rect Plot = new(0, 0, 300, 200);

    private static readonly Size Box = new(10, 8);

    private static ChartDefinition Definition(ChartType type) => new() { Id = "c", Type = type, SourceSheet = "S", SourceRange = "A1:C3" };

    private static ChartFrame Frame(ChartDefinition definition, ChartData data) => new(null!, definition, data, Plot, Plot);

    private static ChartDataLabel ColumnLabel() =>
        new BarPlot().DataLabels(Frame(Definition(ChartType.Column), new ChartData(["A"], [new ChartSeries("S", [4])]))).Single();

    [Fact]
    public void Outside_end_sits_beyond_the_column_top_and_inside_end_below_it()
    {
        var label = ColumnLabel();
        var layout = new ChartLabelLayout();

        var outside = layout.Origin(label, Box, DataLabelPlacement.OutsideEnd);
        var inside = layout.Origin(label, Box, DataLabelPlacement.InsideEnd);

        Assert.True(outside.Y + Box.Height <= label.Anchor.Y);
        Assert.True(inside.Y >= label.Anchor.Y);
    }

    [Fact]
    public void Inside_base_sits_on_the_baseline_and_centre_in_the_middle()
    {
        var label = ColumnLabel();
        var layout = new ChartLabelLayout();

        var inside = layout.Origin(label, Box, DataLabelPlacement.InsideBase);
        var centre = layout.Origin(label, Box, DataLabelPlacement.Center);

        Assert.True(inside.Y + Box.Height <= label.Base!.Value.Y);
        Assert.True(inside.Y + Box.Height > label.Base!.Value.Y - Box.Height - 4);
        Assert.Equal((label.Anchor.Y + label.Base!.Value.Y) / 2, centre.Y + Box.Height / 2, 3);
    }

    [Fact]
    public void Outside_end_pie_labels_leave_the_slice()
    {
        var frame = Frame(Definition(ChartType.Pie), new ChartData(["A", "B"], [new ChartSeries("S", [1, 1])]));
        var label = new PiePlot().DataLabels(frame).First();

        var origin = new ChartLabelLayout().Origin(label, Box, DataLabelPlacement.OutsideEnd);
        var centre = new Point(origin.X + Box.Width / 2, origin.Y + Box.Height / 2);

        var radius = Math.Sqrt(Math.Pow(centre.X - Plot.Center.X, 2) + Math.Pow(centre.Y - Plot.Center.Y, 2));
        Assert.True(radius > Math.Min(Plot.Width, Plot.Height) / 2 - 4);
    }

    [Theory]
    [InlineData(ChartType.PieOfPie)]
    [InlineData(ChartType.BarOfPie)]
    public void Pie_of_pie_moves_the_last_points_into_a_second_plot(ChartType type)
    {
        var plot = new PiePlot();
        var frame = Frame(Definition(type), new ChartData(["A", "B", "C", "D", "E", "F"], [new ChartSeries("S", [10, 8, 6, 3, 2, 1])]));

        var slices = plot.Slices(frame);

        var other = Assert.Single(slices, slice => slice.Index < 0);
        Assert.Equal(3, other.Value);
        var second = slices.Where(slice => slice.Index >= 4).ToList();
        Assert.Equal(2, second.Count);
        Assert.All(second, slice => Assert.True((slice.Box?.Center ?? slice.Centre).X > Plot.Center.X));
        Assert.Equal(type == ChartType.BarOfPie, second.All(slice => slice.Box is not null));
        Assert.Equal(Math.PI * 2, slices.Where(slice => slice.Box is null && slice.Centre == other.Centre).Sum(slice => slice.Sweep), 6);
    }

    [Fact]
    public void A_plain_pie_keeps_one_plot()
    {
        var frame = Frame(Definition(ChartType.Pie), new ChartData(["A", "B", "C"], [new ChartSeries("S", [1, 2, 3])]));

        Assert.All(new PiePlot().Slices(frame), slice => Assert.Equal(Plot.Center, slice.Centre));
    }

    [Fact]
    public void Horizontal_stacked_bar_legends_list_the_series_bottom_up()
    {
        var data = new ChartData(["A"], [new ChartSeries("One", [1]), new ChartSeries("Two", [2])]);

        var stacked = new BarPlot().Legend(Definition(ChartType.StackedBar), data);
        var columns = new BarPlot().Legend(Definition(ChartType.StackedColumn), data);
        var below = new BarPlot().Legend(Definition(ChartType.StackedBar) with { Legend = LegendPosition.Bottom }, data);

        Assert.Equal(["Two", "One"], stacked.Select(item => item.Name));
        Assert.Equal(["One", "Two"], columns.Select(item => item.Name));
        Assert.Equal(["One", "Two"], below.Select(item => item.Name));
    }

    [Fact]
    public void Radar_plots_scale_their_values_without_drawing_axes()
    {
        Assert.True(new RadarPlot().ScalesValues);
        Assert.False(new PiePlot().ScalesValues);
    }
}
