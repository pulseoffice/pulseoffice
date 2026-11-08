using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class TrackedChangesStructureTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static TrackedChange ChangeAt(int row) => new()
    {
        Id = $"c{row}", SheetIndex = 0, Address = new CellAddress(row, 1), Before = CellValue.Empty, After = CellValue.Number(row),
    };

    [Fact]
    public void InsertedRowsMoveTheChangesBelowAndDeletedRowsDropTheirChanges()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Changes.Import([ChangeAt(1), ChangeAt(5)]);
        var structure = new SheetStructure();

        structure.InsertRows(engine, 0, 3, 2);

        Assert.NotNull(workbook.Changes.At(0, new CellAddress(1, 1)));
        Assert.NotNull(workbook.Changes.At(0, new CellAddress(7, 1)));
        Assert.Null(workbook.Changes.At(0, new CellAddress(5, 1)));

        structure.DeleteRows(engine, 0, 7, 1);

        Assert.Equal("c1", Assert.Single(workbook.Changes.Changes).Id);
    }
}
