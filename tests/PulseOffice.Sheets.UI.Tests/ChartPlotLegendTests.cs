using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class ChartPlotLegendTests
{
    private static ChartDefinition Definition(ChartType type) => new() { Id = "c", Type = type, SourceSheet = "Dati", SourceRange = "A1:C3" };

    private static readonly ChartData Data = new(["Nord", "Sud", "Est"],
    [
        new ChartSeries("X", [1, 2, 3]),
        new ChartSeries("Vendite", [4, double.NaN, -2]),
    ]);

    [Fact]
    public void LaLegendaDellaTortaElencaLeCategorie() =>
        Assert.Equal(["Nord", "Sud", "Est"], new PiePlot().Legend(Definition(ChartType.Pie), Data).Select(item => item.Name));

    [Fact]
    public void LaLegendaDellaDispersioneNonMostraLaSerieDelleX() =>
        Assert.Equal(["Vendite"], new ScatterPlot().Legend(Definition(ChartType.Scatter), Data).Select(item => item.Name));

    [Fact]
    public void LeEtichetteDelleBarreSaltanoIVuoti()
    {
        var frame = new ChartFrame(null!, Definition(ChartType.Column), Data, new Avalonia.Rect(0, 0, 300, 200), new Avalonia.Rect(0, 0, 300, 200));

        Assert.Equal(5, new BarPlot().DataLabels(frame).Count());
    }

    [Fact]
    public void LeBarreOrizzontaliPartonoDalBasso()
    {
        var frame = new ChartFrame(null!, Definition(ChartType.Bar), new ChartData(["A", "B"], [new ChartSeries("S", [1, 2])]),
            new Avalonia.Rect(0, 0, 300, 200), new Avalonia.Rect(0, 0, 300, 200));

        var labels = new BarPlot().DataLabels(frame).ToList();

        Assert.True(labels[0].Anchor.Y > labels[1].Anchor.Y);
    }

    [Fact]
    public void GliIstogrammiNonDisegnanoLeCategorieDelleCelle() =>
        Assert.False(new StatisticalPlot().HasCategoryAxis(ChartType.Histogram));
}
