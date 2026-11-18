using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class Round195InlineErrorColumnTests
{
    private readonly ChartInlineErrorValues _errors = new();

    private static ChartDefinition Chart(string reference, IReadOnlyList<ChartHiddenColumn>? hidden = null) => new()
    {
        Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
        InlineData = new ChartData(["A", "B"], [new ChartSeries("S", [1, 2])]) { HiddenColumns = hidden },
        SeriesFormats =
        [
            new ChartSeriesFormat
            {
                ErrorBars = new ChartErrorBars(ErrorBarKind.Custom, 0) { Plus = new ChartErrorValues(reference, [7, 8]) },
            },
        ],
    };

    [Fact]
    public void A_reference_beside_the_series_is_kept_as_a_hidden_column()
    {
        var refreshed = _errors.Refresh(Chart("Sheet1!$D$2:$D$3"));

        var hidden = Assert.Single(refreshed.InlineData!.HiddenColumns!);
        Assert.Equal(3, hidden.Column);
        Assert.Equal([7d, 8d], hidden.Values);
        Assert.Equal([7d, 8d], refreshed.SeriesFormats![0]!.ErrorBars!.Plus!.Values);
    }

    [Fact]
    public void Error_values_refresh_from_their_hidden_column()
    {
        var refreshed = _errors.Refresh(Chart("local-table.$D$2:.$D$3", [new ChartHiddenColumn(3, [0.5, 0.25])]));

        Assert.Equal([0.5, 0.25], refreshed.SeriesFormats![0]!.ErrorBars!.Plus!.Values);
    }

    [Fact]
    public void Remapping_moves_the_reference_to_the_new_column()
    {
        var remapped = _errors.Remap(Chart("local-table.$C$2:.$C$3"), new Dictionary<int, int> { [2] = 4 });

        Assert.Equal("local-table.$E$2:.$E$3", remapped.SeriesFormats![0]!.ErrorBars!.Plus!.Reference);
    }
}
