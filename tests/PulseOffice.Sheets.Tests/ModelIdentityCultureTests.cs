using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class ModelIdentityCultureTests
{
    [Fact]
    public void DatabaseRangeNamesMatchTheSameWayInEveryCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var ranges = new DatabaseRangeCollection();
            ranges.Define(new DatabaseRange { Name = "items", SheetIndex = 0, Range = CellRange.Single(CellAddress.First) });
            Assert.NotNull(ranges.Find("ITEMS"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void AreaLinkWithHugeExtentStaysInsideTheSheet()
    {
        var link = new AreaLink { SheetIndex = 0, Anchor = new CellAddress(10, 10), Url = "x", Rows = int.MaxValue, Columns = int.MaxValue };
        Assert.Equal(new CellAddress(10, 10), link.Area.Start);
        Assert.Equal(new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1), link.Area.End);
    }
}
