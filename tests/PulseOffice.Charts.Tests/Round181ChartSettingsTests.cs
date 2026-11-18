using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round181ChartSettingsTests
{
    private readonly ChartSettingsMapper _mapper = new();

    private static ChartDefinition Chart() =>
        new() { Id = "c", Type = ChartType.Line, SourceSheet = "S", SourceRange = "A1:C4" };

    [Fact]
    public void Unchanged_settings_give_back_an_equal_chart()
    {
        var chart = Chart().WithSeriesFormat(1, new ChartSeriesFormat { Marker = MarkerSymbol.Square }) with
        {
            CategoryAxis = new ChartAxisScale { Reversed = true },
            DataLabels = new ChartDataLabelOptions { ShowPercent = true },
        };

        var applied = _mapper.Apply(chart, _mapper.From(chart));

        Assert.Same(chart.SeriesFormats, applied.SeriesFormats);
        Assert.Equal(chart, applied);
    }

    [Fact]
    public void Axis_scales_round_trip_through_the_settings()
    {
        var category = new ChartAxisScale { Reversed = true, Crossing = AxisCrossing.Maximum };
        var secondary = new ChartAxisScale { Minimum = 0, Maximum = 50, MinorUnit = 5, Crossing = AxisCrossing.Value, CrossingValue = 10, NumberFormat = "0.0" };
        var settings = _mapper.From(Chart()) with { CategoryAxisScale = category, SecondaryValueAxisScale = secondary };

        var applied = _mapper.Apply(Chart(), settings);
        var back = _mapper.From(applied);

        Assert.Equal(category, applied.CategoryAxis);
        Assert.Equal(secondary, applied.SecondaryValueAxis);
        Assert.Equal(secondary, back.SecondaryValueAxisScale);
        Assert.Same(ChartAxisScale.Automatic, _mapper.Apply(applied, back with { CategoryAxisScale = new ChartAxisScale() }).CategoryAxis);
    }

    [Fact]
    public void Series_formats_merge_without_touching_analysis()
    {
        var trend = new ChartTrendline(TrendlineKind.Linear);
        var chart = Chart().WithSeriesFormat(0, new ChartSeriesFormat { Trendline = trend });
        var edited = new[]
        {
            new ChartSeriesFormat { LineWidthPoints = 2.5, Dash = LineDash.Dot },
            new ChartSeriesFormat { Marker = MarkerSymbol.Diamond, MarkerSize = 9, SecondaryAxis = true },
        };

        var applied = _mapper.Apply(chart, _mapper.From(chart) with { SeriesFormats = edited });

        Assert.Equal(2.5, applied.SeriesFormat(0).LineWidthPoints);
        Assert.Equal(LineDash.Dot, applied.SeriesFormat(0).Dash);
        Assert.Equal(trend, applied.SeriesFormat(0).Trendline);
        Assert.Equal(MarkerSymbol.Diamond, applied.SeriesFormat(1).Marker);
        Assert.Equal(9, applied.SeriesFormat(1).MarkerSize);
        Assert.True(applied.HasSecondaryAxis);
        Assert.Equal(applied.SeriesFormat(1), _mapper.From(applied).SeriesFormats![1]);
    }

    [Fact]
    public void Data_label_options_round_trip_and_values_only_is_normalized()
    {
        var options = new ChartDataLabelOptions { ShowValue = false, ShowCategory = true, ShowSeriesName = true, Placement = DataLabelPlacement.OutsideEnd, NumberFormat = "0%" };

        var applied = _mapper.Apply(Chart(), _mapper.From(Chart()) with { ShowDataLabels = true, DataLabels = options });

        Assert.True(applied.ShowDataLabels);
        Assert.Equal(options, _mapper.From(applied).DataLabels);
        Assert.Same(ChartDataLabelOptions.ValuesOnly, _mapper.Apply(applied, _mapper.From(applied) with { DataLabels = new ChartDataLabelOptions() }).DataLabels);
    }
}
