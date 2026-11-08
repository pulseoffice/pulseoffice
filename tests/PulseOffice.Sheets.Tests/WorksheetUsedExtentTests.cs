using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class WorksheetUsedExtentTests
{
    [Fact]
    public void LastUsedIndicesShrinkWhenTrailingCellsAreEmptied()
    {
        var sheet = new Worksheet("S");
        sheet.SetValue(new CellAddress(2, 1), CellValue.Number(1));
        sheet.SetValue(new CellAddress(500, 40), CellValue.Number(2));
        sheet.SetValue(new CellAddress(500, 40), CellValue.Empty);
        Assert.Equal(2, sheet.LastUsedRow);
        Assert.Equal(1, sheet.LastUsedColumn);
        sheet.SetValue(new CellAddress(2, 1), CellValue.Empty);
        Assert.Equal(-1, sheet.LastUsedRow);
        Assert.Equal(-1, sheet.LastUsedColumn);
    }

    [Fact]
    public void OverwritingACellDoesNotCountItTwice()
    {
        var sheet = new Worksheet("S");
        var a = new CellAddress(9, 9);
        sheet.SetValue(a, CellValue.Number(1));
        sheet.SetValue(a, CellValue.Number(2));
        sheet.SetValue(a, CellValue.Empty);
        Assert.Equal(-1, sheet.LastUsedRow);
    }
}
