using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round130ConsolidationUndoTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    [Fact]
    public void Restoring_a_checkpoint_brings_back_a_source_area_a_row_deletion_dropped()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var settings = new ConsolidationSettings
        {
            Sources = [new ConsolidationArea(0, R(5, 0, 7, 1)), new ConsolidationArea(0, R(20, 0, 22, 1))],
            Target = new CellAddress(30, 0),
        };
        workbook.Consolidation = settings;
        var checkpoints = new WorkbookCheckpoints(new SheetSnapshots());
        var before = checkpoints.Capture(workbook);

        new SheetStructure().DeleteRows(engine, 0, 4, 5);
        Assert.Single(workbook.Consolidation!.Sources);
        var after = checkpoints.Capture(workbook);

        checkpoints.Restore(engine, workbook, before);
        Assert.Equal(settings, workbook.Consolidation);
        Assert.Equal(settings.Sources, workbook.Consolidation!.Sources);

        checkpoints.Restore(engine, workbook, after);
        Assert.Equal([new ConsolidationArea(0, R(15, 0, 17, 1))], workbook.Consolidation!.Sources);
        Assert.Equal(new CellAddress(25, 0), workbook.Consolidation.Target);
    }

    [Fact]
    public void Restoring_a_checkpoint_clears_settings_created_after_it()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var checkpoints = new WorkbookCheckpoints(new SheetSnapshots());
        var before = checkpoints.Capture(workbook);
        workbook.Consolidation = new ConsolidationSettings { Sources = [new ConsolidationArea(0, R(0, 0, 1, 1))] };

        checkpoints.Restore(engine, workbook, before);

        Assert.Null(workbook.Consolidation);
    }

    [Fact]
    public void Restoring_a_sheet_snapshot_brings_back_a_source_area_a_row_deletion_dropped()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var settings = new ConsolidationSettings { Sources = [new ConsolidationArea(0, R(5, 0, 7, 1))], Target = new CellAddress(30, 0) };
        workbook.Consolidation = settings;
        var snapshots = new SheetSnapshots();
        var before = snapshots.Capture(workbook.Sheets[0]);

        new SheetStructure().DeleteRows(engine, 0, 4, 5);
        Assert.Empty(workbook.Consolidation!.Sources);

        snapshots.Restore(engine, 0, before);
        Assert.Equal(settings, workbook.Consolidation);
    }
}
