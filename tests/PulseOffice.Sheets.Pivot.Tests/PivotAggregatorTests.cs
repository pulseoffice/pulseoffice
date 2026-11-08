using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class PivotAggregatorTests
{
    private readonly PivotAggregator _aggregator = new();

    private CellValue Of(PivotAggregation aggregation, params double[] numbers) =>
        _aggregator.Aggregate(aggregation, [.. numbers.Select(CellValue.Number)]);

    [Theory]
    [InlineData(PivotAggregation.Median, 2.5)]
    [InlineData(PivotAggregation.Sum, 10)]
    [InlineData(PivotAggregation.Average, 2.5)]
    [InlineData(PivotAggregation.Product, 24)]
    public void LeAggregazioniDiBaseDannoIValoriAttesi(PivotAggregation aggregation, double expected) =>
        Assert.Equal(expected, Of(aggregation, 1, 3, 2, 4).AsNumber(), 6);

    [Fact]
    public void VarianzaEScartoSeguonoLeFormuleDelFoglio()
    {
        double[] data = [2, 4, 4, 4, 5, 5, 7, 9];

        Assert.Equal(2, Of(PivotAggregation.StandardDeviationPopulation, data).AsNumber(), 6);
        Assert.Equal(4, Of(PivotAggregation.VariancePopulation, data).AsNumber(), 6);
        Assert.Equal(32.0 / 7, Of(PivotAggregation.Variance, data).AsNumber(), 6);
    }

    [Fact]
    public void LaVarianzaCampionariaDiUnSoloValoreEUnaDivisionePerZero() =>
        Assert.True(Of(PivotAggregation.Variance, 5).IsError);

    [Fact]
    public void SenzaNumeriLAggregazioneNumericaEVuotaMaIlConteggioEZero()
    {
        Assert.True(_aggregator.Aggregate(PivotAggregation.Sum, [CellValue.Text("a")]).IsEmpty);
        Assert.Equal(0, _aggregator.Aggregate(PivotAggregation.CountNumbers, [CellValue.Text("a")]).AsNumber());
        Assert.Equal(1, _aggregator.Aggregate(PivotAggregation.Count, [CellValue.Text("a")]).AsNumber());
    }
}
