using System.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class MergedRegionsIndexTests
{
    [Fact]
    public void VentimilaUnioniSiAggiungonoELeggonoSenzaCostoQuadratico()
    {
        var merges = new MergedRegions();
        new PulseOffice.Testing.GrowthCheck().Linear(rows =>
        {
            merges = new MergedRegions();
            var current = merges;
            return () =>
            {
                for (var row = 0; row < rows; row += 2)
                    current.Add(new CellRange(new CellAddress(row, 0), new CellAddress(row + 1, 1)));
                for (var row = 0; row < rows; row++)
                    Assert.NotNull(current.At(new CellAddress(row, 1)));
            };
        }, 40_000);

        Assert.Equal(20_000, merges.Count);
    }

    [Fact]
    public void UnUnioneSuColonneInteroSiTrovaInFondoAlFoglio()
    {
        var merges = new MergedRegions();
        var whole = new CellRange(new CellAddress(0, 3), new CellAddress(CellAddress.MaxRows - 1, 4));
        merges.Add(whole);

        Assert.Equal(whole, merges.At(new CellAddress(CellAddress.MaxRows - 1, 4)));
        Assert.True(merges.Remove(whole));
        Assert.Null(merges.At(new CellAddress(500_000, 3)));
    }

    [Fact]
    public void MilleUnioniSuColonneIntereNonOccupanoUnaVocePerBlocco()
    {
        var merges = new MergedRegions();
        new PulseOffice.Testing.GrowthCheck().Linear(columns =>
        {
            merges = new MergedRegions();
            var current = merges;
            return () =>
            {
                for (var column = 0; column < columns; column += 2)
                    current.Add(new CellRange(new CellAddress(0, column), new CellAddress(CellAddress.MaxRows - 1, column + 1)));
                for (var column = 0; column < columns; column++)
                    Assert.NotNull(current.At(new CellAddress(CellAddress.MaxRows - 1 - column, column)));
                for (var column = 0; column < columns; column += 4)
                    Assert.True(current.Remove(new CellRange(new CellAddress(0, column), new CellAddress(CellAddress.MaxRows - 1, column + 1))));
            };
        }, 2_000);

        Assert.Equal(500, merges.Count);
        Assert.Null(merges.At(new CellAddress(700_000, 0)));
        Assert.NotNull(merges.At(new CellAddress(700_000, 2)));
        Assert.Single(merges.Overlapping(new CellRange(new CellAddress(10, 2), new CellAddress(20, 3))));
    }

    [Fact]
    public void LeUnioniSovrappostePrendonoIlPostoDellePrecedenti()
    {
        var merges = new MergedRegions();
        merges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(100, 1)));
        merges.Add(new CellRange(new CellAddress(90, 1), new CellAddress(200, 2)));

        Assert.Null(merges.At(new CellAddress(10, 0)));
        Assert.Equal(1, merges.Count);
    }

    [Fact]
    public void IlFoglioAvanzaLaRevisioneACelleEUnioniMaNonAScrittureUguali()
    {
        var sheet = new Worksheet("S");
        var start = sheet.Revision;

        sheet.SetValue(new CellAddress(0, 0), Values.CellValue.Number(1));
        var dopoCella = sheet.Revision;
        sheet.SetValue(new CellAddress(0, 0), Values.CellValue.Number(1));
        sheet.Merges.Add(new CellRange(new CellAddress(2, 2), new CellAddress(3, 3)));

        Assert.True(dopoCella > start);
        Assert.Equal(dopoCella + 1, sheet.Revision);
    }
}
