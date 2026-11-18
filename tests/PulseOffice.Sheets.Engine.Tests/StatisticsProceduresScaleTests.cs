using System.Diagnostics;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class StatisticsProceduresScaleTests
{
    [Fact]
    public void AnovaSuGruppiGrandiNonEQuadratica()
    {
        var rows = 0;
        new PulseOffice.Testing.GrowthCheck().Linear(size =>
        {
            var first = Enumerable.Range(0, size).Select(index => (double)(index % 7)).ToList();
            var second = Enumerable.Range(0, size).Select(index => (double)(index % 5) + 1).ToList();
            return () => rows = new StatisticsProcedures().AnovaSingleFactor([first, second]).Rows.Count;
        }, 60_000);

        Assert.NotEqual(0, rows);
    }

    [Fact]
    public void FourierSuMoltiCampioniUsaLaTrasformataVeloce()
    {
        var rows = 0;
        new PulseOffice.Testing.GrowthCheck().Linear(size =>
        {
            var values = Enumerable.Range(0, size).Select(index => Math.Sin(index / 10.0)).ToList();
            return () => rows = new StatisticsProcedures().Fourier(values).Rows.Count;
        }, 20_000);

        Assert.Equal(20_000, rows);
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
