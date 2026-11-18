using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round184HorizontalAnalysisTests
{
    private static readonly Rect Plot = new(0, 0, 300, 200);

    private static readonly ChartSeries Series = new("S", [1, 2, 3]);

    private static readonly ChartFrame Frame =
        new(null!, new ChartDefinition { Id = "c", Type = ChartType.Bar, SourceSheet = "S", SourceRange = "A1:B4" },
            new ChartData(["A", "B", "C"], [Series]), Plot, Plot);

    private readonly ChartAnalysisPainter _analysis = new();

    [Fact]
    public void A_trendline_on_horizontal_bars_runs_along_the_value_axis_through_the_bar_ends()
    {
        var plot = new BarPlot();
        var anchors = plot.DataLabels(Frame).ToList();

        var curve = _analysis.TrendPoints(new ChartTrendline(TrendlineKind.Linear), anchors, 0, plot.SeriesScale(Frame, 0)!, horizontal: true);

        var first = anchors.Single(anchor => anchor.Index == 0).Anchor;
        var last = anchors.Single(anchor => anchor.Index == 2).Anchor;
        Assert.Equal(first.X, curve[0].X, 3);
        Assert.Equal(first.Y, curve[0].Y, 3);
        Assert.Equal(last.X, curve[^1].X, 3);
        Assert.Equal(last.Y, curve[^1].Y, 3);
    }

    [Fact]
    public void Value_error_bars_on_horizontal_bars_are_horizontal()
    {
        var plot = new BarPlot();
        var anchors = plot.DataLabels(Frame).ToList();
        var scale = plot.SeriesScale(Frame, 0)!;

        var lines = _analysis.ValueErrors(new ChartErrorBars(ErrorBarKind.FixedValue, 0.5), Series, anchors, scale, horizontal: true);

        Assert.Equal(3, lines.Count);
        var bar = anchors.Single(anchor => anchor.Index == 1).Anchor;
        var line = lines[1];
        Assert.Equal(bar.Y, line.From.Y, 3);
        Assert.Equal(bar.Y, line.To.Y, 3);
        Assert.Equal(scale.Position(1.5), line.From.X, 3);
        Assert.Equal(scale.Position(2.5), line.To.X, 3);
    }
}
