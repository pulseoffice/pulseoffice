using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class Round327PieSplitTests
{
    private readonly IPieSplitter _splitter = new PieSplitter();

    private static readonly int[] Plotted = [0, 1, 2, 3, 4, 5];

    private static readonly double[] Values = [40, 30, 15, 8, 4, 3];

    [Fact]
    public void Automatic_puts_the_last_third_in_the_second_plot() =>
        Assert.Equal(new[] { 4, 5 }, _splitter.SecondPlot(ChartPieSplit.Automatic, Plotted, Values));

    [Fact]
    public void Position_takes_the_last_points() =>
        Assert.Equal(new[] { 3, 4, 5 }, _splitter.SecondPlot(new ChartPieSplit { Kind = PieSplitKind.Position, Threshold = 3 }, Plotted, Values));

    [Fact]
    public void Value_takes_the_points_below_it() =>
        Assert.Equal(new[] { 3, 4, 5 }, _splitter.SecondPlot(new ChartPieSplit { Kind = PieSplitKind.Value, Threshold = 10 }, Plotted, Values));

    [Fact]
    public void Percent_takes_the_points_below_that_share() =>
        Assert.Equal(new[] { 4, 5 }, _splitter.SecondPlot(new ChartPieSplit { Kind = PieSplitKind.Percent, Threshold = 5 }, Plotted, Values));

    [Fact]
    public void Custom_takes_the_chosen_points() =>
        Assert.Equal(new[] { 1, 4 }, _splitter.SecondPlot(new ChartPieSplit { Kind = PieSplitKind.Custom, CustomPoints = [4, 1] }, Plotted, Values));

    [Fact]
    public void A_split_that_takes_every_point_or_none_draws_one_pie() =>
        Assert.Empty(_splitter.SecondPlot(new ChartPieSplit { Kind = PieSplitKind.Value, Threshold = 1000 }, Plotted, Values));

    [Fact]
    public void The_settings_mapper_carries_the_split()
    {
        var mapper = new ChartSettingsMapper();
        var split = new ChartPieSplit { Kind = PieSplitKind.Position, Threshold = 2, SecondPlotSize = 120 };
        var chart = new ChartDefinition { Id = "c", Type = ChartType.PieOfPie, SourceSheet = "S", SourceRange = "A1:B4", PieSplit = split };

        Assert.Equal(split, mapper.Apply(chart with { PieSplit = ChartPieSplit.Automatic }, mapper.From(chart)).PieSplit);
    }
}
