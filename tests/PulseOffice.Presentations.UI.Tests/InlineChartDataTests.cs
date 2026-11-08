using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class InlineChartDataTests
{
    private sealed class FixedSamples : IChartSampleData
    {
        public ChartData For(ChartFamily family) => new(["A", "B"], [new ChartSeries(family.ToString(), [1, 2])]);
    }

    private readonly InlineChartData _data = new(new FixedSamples(), new ChartFamilyCatalog());

    private static ChartDefinition Chart(ChartType type, ChartData? data) =>
        new() { Id = "c", Type = type, SourceSheet = "", SourceRange = "", InlineData = data };

    [Fact]
    public void UnGraficoSenzaDatiModificatoConservaIDatiMostratiNellEditor()
    {
        var chart = Chart(ChartType.Pie, null);

        var settled = _data.Settle(chart);

        Assert.NotNull(settled.InlineData);
        Assert.Equal(_data.For(chart).Categories, settled.InlineData!.Categories);
        Assert.Equal(2, settled.InlineData.Series[0].Values.Count);
    }

    [Fact]
    public void IDatiPropriDelGraficoNonSiSostituiscono()
    {
        var own = new ChartData(["Gen"], [new ChartSeries("Nord", [7])]);

        var settled = _data.Settle(Chart(ChartType.Column, own));

        Assert.Same(own, settled.InlineData);
    }
}
