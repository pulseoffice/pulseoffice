using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class DeepAuditChartTests
{
    [Fact]
    public void UnIstogrammaSuValoriAgliEstremiHaClassiFinite()
    {
        var bins = new ChartStatistics().Histogram([-1e308, 0, 1e308]);

        Assert.All(bins, bin => Assert.True(double.IsFinite(bin.Lower) && double.IsFinite(bin.Upper)));
        Assert.Equal(3, bins.Sum(bin => bin.Count));
    }

    [Fact]
    public void UnaDensitaSuValoriAgliEstremiNonDaNaN()
    {
        var density = new ChartStatistics().Density([-1e308, 0, 1e308, 5e307]);

        Assert.All(density, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Fact]
    public async Task LeDivisioniConUnTettoNonPositivoTerminano()
    {
        var task = Task.Run(() => new PlotScale(0, 100, 0, 1).Divisions(-1), TestContext.Current.CancellationToken);

        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Same(task, finished);
        Assert.NotEmpty(await task);
    }
}
