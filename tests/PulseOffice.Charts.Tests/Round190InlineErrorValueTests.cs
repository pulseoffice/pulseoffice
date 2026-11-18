using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round190InlineErrorValueTests
{
    private readonly ChartInlineErrorValues _refresh = new();

    private static ChartDefinition Chart(string reference, params double[] cached) => new()
    {
        Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
        InlineData = new ChartData(["A", "B"], [new ChartSeries("S", [1, 2]), new ChartSeries("E", [0.5, 0.25])]),
        SeriesFormats =
        [
            new ChartSeriesFormat
            {
                ErrorBars = new ChartErrorBars(ErrorBarKind.Custom, 0)
                {
                    Plus = new ChartErrorValues(reference, cached),
                    Minus = new ChartErrorValues(null, [9]),
                },
            },
        ],
    };

    [Theory]
    [InlineData("Sheet1!$C$2:$C$3")]
    [InlineData("local-table.$C$2:.$C$3")]
    public void Custom_error_values_follow_the_chart_data_table(string reference)
    {
        var bars = _refresh.Refresh(Chart(reference, 7, 7)).SeriesFormats![0]!.ErrorBars!;

        Assert.Equal([0.5, 0.25], bars.Plus!.Values);
        Assert.Equal([9.0], bars.Minus!.Values);
    }

    [Fact]
    public void References_outside_the_table_keep_their_cache()
    {
        var chart = Chart("Sheet1!$D$2:$D$3", 7, 7);

        Assert.Same(chart, _refresh.Refresh(chart));
    }
}
