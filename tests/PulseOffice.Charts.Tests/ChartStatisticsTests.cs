using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class ChartStatisticsTests
{
    private static readonly IChartStatistics Statistics = new ChartStatistics();

    private static readonly double[] Sample = [2, 4, 4, 4, 5, 5, 7, 9];

    [Fact]
    public void SenzaValoriNonCiSonoIntervalli()
    {
        Assert.Empty(Statistics.Histogram([]));
    }

    [Fact]
    public void GliIntervalliContengonoTuttiIValori()
    {
        var bins = Statistics.Histogram(Sample, 4);

        Assert.Equal(4, bins.Count);
        Assert.Equal(Sample.Length, bins.Sum(bin => bin.Count));
    }

    [Fact]
    public void ValoriTuttiUgualiFannoUnIntervalloSolo()
    {
        var bins = Statistics.Histogram([3, 3, 3]);

        Assert.Single(bins);
        Assert.Equal(3, bins[0].Count);
    }

    [Fact]
    public void SenzaNumeroDiIntervalliSiUsaLaRadice()
    {
        Assert.Equal(3, Statistics.Histogram(Sample).Count);
    }

    [Fact]
    public void ICinqueNumeriDescrivonoLaDistribuzione()
    {
        var summary = Statistics.Summary(Sample);

        Assert.Equal(2, summary.Minimum);
        Assert.Equal(9, summary.Maximum);
        Assert.Equal(4.5, summary.Median, 6);
        Assert.Equal(4, summary.LowerQuartile, 6);
        Assert.Equal(5.5, summary.UpperQuartile, 6);
    }

    [Fact]
    public void IValoriAnomaliStannoFuoriDaUnaVoltaEMezzaLoScarto()
    {
        var outliers = Statistics.Outliers([1, 2, 2, 2, 3, 3, 3, 40]);

        Assert.Contains(40, outliers);
        Assert.DoesNotContain(3, outliers);
    }

    [Fact]
    public void SenzaDispersioneNonCiSonoAnomali()
    {
        Assert.Empty(Statistics.Outliers([5, 5, 5, 5]));
    }

    [Fact]
    public void LaDensitaEPositivaESiIntegraACirca_uno()
    {
        var points = Statistics.Density(Sample, 200);

        Assert.NotEmpty(points);
        Assert.All(points, point => Assert.True(point.Y >= 0));

        var area = 0.0;

        for (var index = 1; index < points.Count; index++)
            area += (points[index].X - points[index - 1].X)
                * (points[index].Y + points[index - 1].Y) / 2;

        Assert.Equal(1, area, 1);
    }

    [Fact]
    public void LaCumulataArrivaAUno()
    {
        var points = Statistics.Cumulative(Sample);

        Assert.Equal(Sample.Length, points.Count);
        Assert.Equal(1, points[^1].Y, 6);
        Assert.True(points[0].Y > 0);
    }

    [Fact]
    public void LaCumulataNonScendeMai()
    {
        var points = Statistics.Cumulative(Sample);

        for (var index = 1; index < points.Count; index++)
            Assert.True(points[index].Y >= points[index - 1].Y);
    }

    [Fact]
    public void ILeQuantiliTeoriciSonoSimmetrici()
    {
        var pairs = Statistics.QuantilePairs([1, 2, 3, 4]);

        Assert.Equal(4, pairs.Count);
        Assert.Equal(-pairs[0].Theoretical, pairs[^1].Theoretical, 6);
    }

    [Fact]
    public void ITotaliProgressiviSommanoInOrdine()
    {
        Assert.Equal([1d, 3, 6, 10], Statistics.RunningTotals([1, 2, 3, 4]));
    }

    [Fact]
    public void ITotaliProgressiviReggonoIValoriNegativi()
    {
        Assert.Equal([5d, 2, 6], Statistics.RunningTotals([5, -3, 4]));
    }
    [Fact]
    public void ValoriTuttiUgualiHannoUnIntervalloDiLarghezzaNominale()
    {
        var bins = Statistics.Histogram([3, 3, 3]);

        Assert.True(bins[0].Upper > bins[0].Lower);
        Assert.InRange(3, bins[0].Lower, bins[0].Upper);
    }

    [Fact]
    public void LaBandaDellaDensitaSegueSilverman()
    {
        var summary = Statistics.Summary(Sample);
        var mean = Sample.Average();
        var deviation = Math.Sqrt(Sample.Sum(value => Math.Pow(value - mean, 2)) / (Sample.Length - 1));
        var expected = 0.9 * Math.Min(deviation, summary.InterQuartile / 1.34) * Math.Pow(Sample.Length, -0.2);

        var points = Statistics.Density(Sample, 64);

        Assert.Equal(2 - (3 * expected), points[0].X, 9);
    }
}

public class PlotScaleTests
{
    [Fact]
    public void LaScalaMappaGliEstremi()
    {
        var scale = new PlotScale(0, 10, 100, 200);

        Assert.Equal(100, scale.Position(0), 6);
        Assert.Equal(200, scale.Position(10), 6);
        Assert.Equal(150, scale.Position(5), 6);
    }

    [Fact]
    public void LaScalaSiInverteSenzaPerdite()
    {
        var scale = new PlotScale(-5, 15, 0, 400);

        Assert.Equal(7, scale.Value(scale.Position(7)), 6);
    }

    [Fact]
    public void UnaScalaSenzaAmpiezzaNonDivideMaiPerZero()
    {
        var scale = new PlotScale(4, 4, 0, 100);

        Assert.Equal(0, scale.Position(4), 6);
    }

    [Fact]
    public void LaScalaGradevoleArrotondaGliEstremi()
    {
        var scale = PlotScale.Nice(3, 97, 0, 100);

        Assert.True(scale.Minimum <= 3);
        Assert.True(scale.Maximum >= 97);
        Assert.Equal(0, scale.Minimum, 6);
    }

    [Fact]
    public void LaScalaGradevoleNonScendeSottoZeroSeIDatiSonoPositivi()
    {
        Assert.True(PlotScale.Nice(10, 20, 0, 100).Minimum >= 0);
    }

    [Fact]
    public void ConValoriUgualiLaScalaSiAllarga()
    {
        var scale = PlotScale.Nice(5, 5, 0, 100);

        Assert.True(scale.Maximum > scale.Minimum);
    }

    [Fact]
    public void LeTaccheSonoEquidistanti()
    {
        var ticks = new PlotScale(0, 100, 0, 1).Ticks(5);

        Assert.Equal(5, ticks.Count);
        Assert.Equal(25, ticks[1] - ticks[0], 6);
    }

    [Fact]
    public void LeCategorieSiDividonoInFette()
    {
        var scale = new CategoryScale(4, 0, 400);

        Assert.Equal(100, scale.Width, 6);
        Assert.Equal(50, scale.Centre(0), 6);
        Assert.Equal(300, scale.Start(3), 6);
    }

    [Fact]
    public void LeSerieSiDividonoLaFettaSenzaSovrapporsi()
    {
        var scale = new CategoryScale(2, 0, 200);

        var first = scale.Slot(0, 0, 3);
        var second = scale.Slot(0, 1, 3);

        Assert.True(first.Start + first.Width <= second.Start + 0.001);
    }

}
