using System.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SparklineDataRangeTests
{
    [Fact]
    public void UnaColonnaInteraComeSorgenteLeggeSoloLeRigheUsate()
    {
        SparklinePoints points = null!;
        new PulseOffice.Testing.GrowthCheck().Flat(rows =>
        {
            var sheet = new Worksheet("Dati");
            sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
            sheet.SetValue(new CellAddress(1, 0), CellValue.Number(4));
            var source = new CellRange(new CellAddress(0, 0), new CellAddress(rows - 1, 0));
            var sparkline = new Sparkline(new CellAddress(0, 2), source);
            var group = new SparklineGroup { Id = "g", Sparklines = [sparkline, sparkline, sparkline], SameScale = true };
            return () => points = new SparklineData().Read(sheet, group, sparkline);
        }, 16, CellAddress.MaxRows);

        Assert.Equal([1d, 4d], points.Values);
        Assert.Equal(4d, points.Maximum);
    }
}
