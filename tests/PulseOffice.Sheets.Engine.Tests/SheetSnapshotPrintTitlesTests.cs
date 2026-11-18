using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class SheetSnapshotPrintTitlesTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void Restoring_a_checkpoint_brings_back_the_repeated_print_rows_and_columns()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        var checkpoints = new WorkbookCheckpoints(new SheetSnapshots());
        var before = checkpoints.Capture(workbook);

        sheet.PrintTitleRows = new PulseOffice.Sheets.PrintTitleBand(0, 3);
        sheet.PrintTitleColumns = new PulseOffice.Sheets.PrintTitleBand(0, 2);
        var after = checkpoints.Capture(workbook);

        checkpoints.Restore(engine, workbook, before);
        Assert.Equal(0, sheet.PrintTitleRows.Count);
        Assert.Equal(0, sheet.PrintTitleColumns.Count);

        checkpoints.Restore(engine, workbook, after);
        Assert.Equal(3, sheet.PrintTitleRows.Count);
        Assert.Equal(2, sheet.PrintTitleColumns.Count);
    }
}
