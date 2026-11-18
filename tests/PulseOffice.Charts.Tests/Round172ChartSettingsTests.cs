using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round172ChartSettingsTests
{
    [Fact]
    public void Changing_the_trendline_keeps_each_series_own_error_bars()
    {
        var mapper = new ChartSettingsMapper();
        var custom = new ChartErrorBars(ErrorBarKind.Custom, 1) { Plus = new ChartErrorValues("S!B2:B4", [1, 2, 3]) };
        var fixedBars = new ChartErrorBars(ErrorBarKind.FixedValue, 4);
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Line, SourceSheet = "S", SourceRange = "A1:C4" }
            .WithSeriesFormat(0, new ChartSeriesFormat { ErrorBars = custom })
            .WithSeriesFormat(1, new ChartSeriesFormat { ErrorBars = fixedBars });

        var changed = mapper.Apply(chart, mapper.From(chart) with { SeriesCount = 2, Trendline = new ChartTrendline(TrendlineKind.Linear) });

        Assert.Equal(custom, changed.SeriesFormat(0).ErrorBars);
        Assert.Equal(fixedBars, changed.SeriesFormat(1).ErrorBars);
        Assert.Equal(TrendlineKind.Linear, changed.SeriesFormat(1).Trendline?.Kind);
    }
}
