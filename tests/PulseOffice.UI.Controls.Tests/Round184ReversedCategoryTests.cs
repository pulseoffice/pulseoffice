using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round184ReversedCategoryTests
{
    private static readonly Rect Plot = new(0, 0, 300, 200);

    private static readonly ChartData Data = new(["A", "B"], [new ChartSeries("S", [1, 2])]);

    private static ChartFrame Frame(ChartType type, bool reversed) =>
        new(null!, new ChartDefinition
        {
            Id = "c", Type = type, SourceSheet = "S", SourceRange = "A1:B3",
            CategoryAxis = new ChartAxisScale { Reversed = reversed },
        }, Data, Plot, Plot);

    private static double Across(IChartPlot plot, ChartFrame frame, int index, bool horizontal)
    {
        var anchor = plot.DataLabels(frame).Single(label => label.Index == index).Anchor;
        return horizontal ? anchor.Y : anchor.X;
    }

    [Theory]
    [InlineData(ChartType.Column, false)]
    [InlineData(ChartType.ColumnLine, false)]
    [InlineData(ChartType.StackedColumn, false)]
    [InlineData(ChartType.Bar, true)]
    [InlineData(ChartType.StackedBar, true)]
    public void Reversed_categories_swap_the_bar_order(ChartType type, bool horizontal)
    {
        var plot = new BarPlot();

        var normal = Frame(type, false);
        var reversed = Frame(type, true);

        Assert.True(Math.Sign(Across(plot, normal, 1, horizontal) - Across(plot, normal, 0, horizontal))
            == -Math.Sign(Across(plot, reversed, 1, horizontal) - Across(plot, reversed, 0, horizontal)));
    }

    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    public void Reversed_categories_swap_the_line_order(ChartType type)
    {
        var plot = new LinePlot();

        Assert.True(Across(plot, Frame(type, false), 0, false) < Across(plot, Frame(type, false), 1, false));
        Assert.True(Across(plot, Frame(type, true), 0, false) > Across(plot, Frame(type, true), 1, false));
    }
}
