using System.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SheetRegionsTests
{
    [Fact]
    public void LaRegioneDiUnaColonnaLungaSiTrovaInTempoLineare()
    {
        var sheet = new Worksheet("Dati");
        for (var row = 0; row < 40_000; row++) sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row));

        var watch = Stopwatch.StartNew();
        var region = new SheetRegions().CurrentRegion(sheet, new CellAddress(20_000, 1));
        watch.Stop();

        Assert.Equal(new CellRange(new CellAddress(0, 1), new CellAddress(39_999, 1)), region);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
    }

    [Fact]
    public void LaRegioneCresceAncheInDiagonale()
    {
        var sheet = new Worksheet("Dati");
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(1));
        sheet.SetValue(new CellAddress(2, 2), CellValue.Number(2));
        sheet.SetValue(new CellAddress(3, 3), CellValue.Number(3));

        var region = new SheetRegions().CurrentRegion(sheet, new CellAddress(1, 1));

        Assert.Equal(new CellRange(new CellAddress(1, 1), new CellAddress(3, 3)), region);
    }
}
