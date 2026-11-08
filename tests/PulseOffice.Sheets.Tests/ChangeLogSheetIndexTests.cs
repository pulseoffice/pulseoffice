using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class ChangeLogSheetIndexTests
{
    private static Workbook ThreeSheetsWithAChangeOnTheSecond()
    {
        var workbook = Workbook.CreateDefault();
        while (workbook.Sheets.Count < 3) workbook.AddSheet(workbook.UniqueSheetName("S"));
        workbook.Changes.Import([new TrackedChange
        {
            Id = "c1", SheetIndex = 1, Address = new CellAddress(2, 2), Before = CellValue.Empty, After = CellValue.Number(1),
        }]);
        return workbook;
    }

    [Fact]
    public void MovingSheetsCarriesTheChangeWithItsSheet()
    {
        var workbook = ThreeSheetsWithAChangeOnTheSecond();
        var owner = workbook.Sheets[1];

        workbook.MoveSheet(1, 2);

        Assert.Equal(workbook.Sheets.ToList().IndexOf(owner), Assert.Single(workbook.Changes.Changes).SheetIndex);
        Assert.NotNull(workbook.Changes.At(2, new CellAddress(2, 2)));
        Assert.Null(workbook.Changes.At(1, new CellAddress(2, 2)));
    }

    [Fact]
    public void RelocatingMovedCellsCarriesTheirChangesToTheTargetSheetAndLeavesTheOthers()
    {
        var workbook = ThreeSheetsWithAChangeOnTheSecond();

        workbook.Changes.Relocate(1, 2, address => address.Row == 2 ? new CellAddress(7, address.Column) : null);
        workbook.Changes.Relocate(0, 1, address => new CellAddress(0, 0));

        Assert.Null(workbook.Changes.At(1, new CellAddress(2, 2)));
        Assert.Equal("c1", workbook.Changes.At(2, new CellAddress(7, 2))!.Id);
    }

    [Fact]
    public void InsertingASheetBeforeShiftsTheChange()
    {
        var workbook = ThreeSheetsWithAChangeOnTheSecond();

        workbook.InsertSheet("New", 0);

        Assert.Equal(2, Assert.Single(workbook.Changes.Changes).SheetIndex);
    }

    [Fact]
    public void RemovingTheSheetDetachesItsChangesAndReinsertingBringsThemBack()
    {
        var workbook = ThreeSheetsWithAChangeOnTheSecond();
        var owner = workbook.Sheets[1];

        workbook.RemoveSheet(owner);
        Assert.Empty(workbook.Changes.Changes);

        workbook.InsertSheet(owner, 0);
        Assert.Equal(0, Assert.Single(workbook.Changes.Changes).SheetIndex);
    }

    [Fact]
    public void RemovingAnEarlierSheetShiftsTheChangeBack()
    {
        var workbook = ThreeSheetsWithAChangeOnTheSecond();

        workbook.RemoveSheet(workbook.Sheets[0]);

        Assert.Equal(0, Assert.Single(workbook.Changes.Changes).SheetIndex);
    }
}
