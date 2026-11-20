using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round335SparklineSourceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly CellRange Row = new(new CellAddress(1, 0), new CellAddress(1, 3));

    private readonly SheetStructure _structure = new();

    private (Workbook Workbook, Recalculation.CalculationEngine Engine, Worksheet Host) Foreign()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var host = workbook.Sheets[0];
        workbook.AddSheet("Source").SetValue(new CellAddress(1, 0), CellValue.Number(1));
        host.Sparklines.Add(new SparklineGroup
        {
            Id = "g",
            Sparklines = [new Sparkline(new CellAddress(0, 5), Row, "Source")],
            DateRange = Row,
            DateSheet = "Source",
        });
        return (workbook, engine, host);
    }

    [Fact]
    public void Inserting_rows_on_the_source_sheet_moves_a_foreign_source_and_its_dates()
    {
        var (_, engine, host) = Foreign();

        _structure.InsertRows(engine, 1, 0, 2);

        var group = host.Sparklines.Groups.Single();
        Assert.Equal(new CellRange(new CellAddress(3, 0), new CellAddress(3, 3)), group.Sparklines[0].Source);
        Assert.Equal(new CellAddress(0, 5), group.Sparklines[0].Cell);
        Assert.Equal(new CellRange(new CellAddress(3, 0), new CellAddress(3, 3)), group.DateRange);
    }

    [Fact]
    public void Inserting_rows_on_the_host_moves_the_cell_but_not_a_foreign_source()
    {
        var (_, engine, host) = Foreign();

        _structure.InsertRows(engine, 0, 0, 1);

        var line = host.Sparklines.Groups.Single().Sparklines[0];
        Assert.Equal(new CellAddress(1, 5), line.Cell);
        Assert.Equal(Row, line.Source);
    }

    [Fact]
    public void Deleting_the_rows_of_a_foreign_source_removes_the_sparkline()
    {
        var (_, engine, host) = Foreign();

        _structure.DeleteRows(engine, 1, 1, 1);

        Assert.Equal(0, host.Sparklines.Count);
    }

    [Fact]
    public void Deleting_columns_on_the_host_shrinks_a_local_source_and_its_local_dates()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var host = workbook.Sheets[0];
        host.Sparklines.Add(new SparklineGroup { Id = "g", Sparklines = [new Sparkline(new CellAddress(0, 5), Row)], DateRange = Row });

        _structure.DeleteColumns(engine, 0, 1, 1);

        var group = host.Sparklines.Groups.Single();
        var shrunk = new CellRange(new CellAddress(1, 0), new CellAddress(1, 2));
        Assert.Equal(shrunk, group.Sparklines[0].Source);
        Assert.Equal(new CellAddress(0, 4), group.Sparklines[0].Cell);
        Assert.Equal(shrunk, group.DateRange);
    }

    [Fact]
    public void Removing_the_source_sheet_drops_its_sparklines_and_undo_brings_them_back()
    {
        var (workbook, _, host) = Foreign();
        var references = new ChartSheetReferences();
        var source = workbook.Sheets[1];

        var changes = references.Removed(workbook, source);
        workbook.RemoveSheet(source);
        references.Apply(changes, forward: true);

        Assert.Equal(0, host.Sparklines.Count);

        workbook.InsertSheet(source, 1);
        references.Apply(changes, forward: false);

        Assert.Equal("Source", host.Sparklines.Groups.Single().Sparklines[0].SourceSheet);
    }

    [Fact]
    public void Removing_the_date_sheet_keeps_the_sparkline_without_dates()
    {
        var (workbook, _) = fixture.CreateWorkbook();
        var host = workbook.Sheets[0];
        var dates = workbook.AddSheet("Dates");
        host.Sparklines.Add(new SparklineGroup
        {
            Id = "g", Sparklines = [new Sparkline(new CellAddress(0, 5), Row)], DateRange = Row, DateSheet = "Dates",
        });
        var references = new ChartSheetReferences();

        references.Apply(references.Removed(workbook, dates), forward: true);

        var group = host.Sparklines.Groups.Single();
        Assert.Null(group.DateRange);
        Assert.Null(group.DateSheet);
    }
}
