using System.Diagnostics;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class StatisticsProceduresScaleTests
{
    [Fact]
    public void AnovaSuGruppiGrandiNonEQuadratica()
    {
        var first = Enumerable.Range(0, 60_000).Select(index => (double)(index % 7)).ToList();
        var second = Enumerable.Range(0, 60_000).Select(index => (double)(index % 5) + 1).ToList();

        var watch = Stopwatch.StartNew();
        var table = new StatisticsProcedures().AnovaSingleFactor([first, second]);
        watch.Stop();

        Assert.NotEmpty(table.Rows);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
    }

    [Fact]
    public void FourierSuMoltiCampioniUsaLaTrasformataVeloce()
    {
        var values = Enumerable.Range(0, 20_000).Select(index => Math.Sin(index / 10.0)).ToList();

        var watch = Stopwatch.StartNew();
        var table = new StatisticsProcedures().Fourier(values);
        watch.Stop();

        Assert.Equal(20_000, table.Rows.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
    }

    [Fact]
    public void FourierPiccolaResta()
    {
        var table = new StatisticsProcedures().Fourier([1, 2, 3, 4]);

        Assert.Equal(10d, table.Rows[0].Values[0].AsNumber(), 9);
        Assert.Equal(-2d, table.Rows[1].Values[0].AsNumber(), 9);
        Assert.Equal(2d, table.Rows[1].Values[1].AsNumber(), 9);
        Assert.Equal(0d, table.Rows[2].Values[1].AsNumber(), 9);
    }
}
