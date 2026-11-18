using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round168ChartScaleTests
{
    private readonly ChartAxisScaling _scaling = new();

    [Fact]
    public void An_automatic_axis_keeps_the_automatic_scale()
    {
        var automatic = new PlotScale(0, 100, 200, 0);

        Assert.Same(automatic, _scaling.Apply(automatic, ChartAxisScale.Automatic));
    }

    [Fact]
    public void Fixed_bounds_and_reversal_change_the_positions()
    {
        var scale = _scaling.Apply(new PlotScale(0, 100, 200, 0), new ChartAxisScale { Minimum = 20, Maximum = 60, Reversed = true });

        Assert.Equal(20, scale.Minimum);
        Assert.Equal(60, scale.Maximum);
        Assert.Equal(0, scale.Position(20), 6);
        Assert.Equal(200, scale.Position(60), 6);
    }

    [Fact]
    public void A_logarithmic_axis_places_powers_evenly_and_ticks_on_them()
    {
        var scale = _scaling.Apply(new PlotScale(0, 1000, 0, 300), new ChartAxisScale { Logarithmic = true, Minimum = 1, Maximum = 1000 });

        Assert.Equal(100, scale.Position(10), 6);
        Assert.Equal(200, scale.Position(100), 6);
        Assert.Equal([1d, 10d, 100d, 1000d], scale.Divisions());
    }

    [Fact]
    public void A_major_unit_sets_the_divisions()
    {
        var scale = _scaling.Apply(new PlotScale(0, 100, 0, 100), new ChartAxisScale { MajorUnit = 25 });

        Assert.Equal([0d, 25d, 50d, 75d, 100d], scale.Divisions());
    }

    [Fact]
    public void A_linear_trendline_fits_a_straight_series_exactly()
    {
        var curve = new ChartTrendFit().Curve(new ChartTrendline(TrendlineKind.Linear) { Forward = 1 }, [(1, 3), (2, 5), (3, 7)], 5);

        Assert.Equal(5, curve.Count);
        Assert.Equal(4, curve[^1].X, 6);
        Assert.Equal(9, curve[^1].Y, 6);
    }

    [Fact]
    public void A_polynomial_trendline_follows_a_parabola()
    {
        var curve = new ChartTrendFit().Curve(new ChartTrendline(TrendlineKind.Polynomial) { Order = 2 }, [(0, 0), (1, 1), (2, 4), (3, 9)], 4);

        Assert.All(curve, point => Assert.Equal(point.X * point.X, point.Y, 6));
    }

    [Fact]
    public void Percentage_error_bars_span_the_value_share()
    {
        var spans = new ChartErrorSpans().Spans(new ChartErrorBars(ErrorBarKind.Percentage, 10), [50, double.NaN]);

        Assert.Equal(new ChartErrorSpan(50, 45, 55), spans[0]);
        Assert.Null(spans[1]);
    }

    [Fact]
    public void The_editor_settings_apply_a_trendline_to_every_series_in_one_change()
    {
        var mapper = new ChartSettingsMapper();
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Line, SourceSheet = "S", SourceRange = "A1:C4" };

        Assert.Equal(chart, mapper.Apply(chart, mapper.From(chart)));

        var changed = mapper.Apply(chart, mapper.From(chart) with
        {
            SeriesCount = 2,
            Trendline = new ChartTrendline(TrendlineKind.Linear),
            ValueAxisScale = new ChartAxisScale { Maximum = 10 },
        });

        Assert.Equal(TrendlineKind.Linear, changed.SeriesFormat(0).Trendline?.Kind);
        Assert.Equal(TrendlineKind.Linear, changed.SeriesFormat(1).Trendline?.Kind);
        Assert.Equal(10, changed.ValueAxis.Maximum);
    }
}
