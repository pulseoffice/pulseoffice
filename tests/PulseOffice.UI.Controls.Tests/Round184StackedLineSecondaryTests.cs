using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round184StackedLineSecondaryTests
{
    private static readonly Rect Plot = new(0, 0, 300, 200);

    private static readonly ChartData Data = new(["A", "B"], [new ChartSeries("Small", [1, 2]), new ChartSeries("Large", [100, 200])]);

    private static ChartFrame Frame(ChartType type) =>
        new(null!, new ChartDefinition { Id = "c", Type = type, SourceSheet = "S", SourceRange = "A1:C3" }
            .WithSeriesFormat(1, new ChartSeriesFormat { SecondaryAxis = true }), Data, Plot, Plot);

    [Theory]
    [InlineData(ChartType.StackedLine)]
    [InlineData(ChartType.StackedArea)]
    public void Stacked_lines_and_areas_split_onto_the_secondary_axis(ChartType type)
    {
        var plot = new LinePlot();
        var frame = Frame(type);

        var primary = plot.ValueAxis(frame)!;
        var secondary = plot.SecondaryValueAxis(frame);

        Assert.NotNull(secondary);
        Assert.True(primary.Maximum < 10);
        Assert.True(secondary.Maximum >= 200);
        var large = plot.DataLabels(frame).Single(label => label.Series == 1 && label.Index == 0);
        Assert.Equal(secondary.Position(100), large.Anchor.Y, 3);
    }

    [Fact]
    public void Each_side_stacks_to_its_own_hundred_percent()
    {
        var plot = new LinePlot();
        var frame = Frame(ChartType.PercentStackedArea);

        var labels = plot.DataLabels(frame).Where(label => label.Index == 0).ToList();

        Assert.Equal(plot.ValueAxis(frame)!.Position(100), labels.Single(label => label.Series == 0).Anchor.Y, 3);
        Assert.Equal(plot.SecondaryValueAxis(frame)!.Position(100), labels.Single(label => label.Series == 1).Anchor.Y, 3);
    }
}
