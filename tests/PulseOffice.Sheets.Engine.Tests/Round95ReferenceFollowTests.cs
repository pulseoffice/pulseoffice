using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round95ReferenceFollowTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly SheetStructure _structure = new();

    private SheetCellShift Shifts => new(_structure);

    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private static CellAddress A(int row, int column) => new(row, column);

    [Fact]
    public void A_named_range_follows_inserted_rows_and_columns_and_breaks_when_deleted()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var name = workbook.Sheets[0].Name;
        workbook.DefineName(new DefinedName("Data", $"{name}.$B$2:$B$4"));
        workbook.DefineName(new DefinedName("Local", "B2", name));

        _structure.InsertRows(engine, 0, 0, 2);
        _structure.InsertColumns(engine, 0, 0, 1);

        Assert.Equal($"{name}.$C$4:$C$6", workbook.FindName("Data")!.Expression);
        Assert.Equal("C4", workbook.FindName("Local", name)!.Expression);

        _structure.DeleteColumns(engine, 0, 2, 1);
        Assert.Contains("#REF!", workbook.FindName("Data")!.Expression, StringComparison.Ordinal);
    }

    [Fact]
    public void A_global_name_with_an_unqualified_reference_is_left_to_the_sheet_using_it()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.DefineName(new DefinedName("Here", "B2"));

        _structure.InsertRows(engine, 0, 0, 1);

        Assert.Equal("B2", workbook.FindName("Here")!.Expression);
    }

    [Fact]
    public void Cells_shifted_down_move_the_autofilter_named_range_and_print_range_of_the_band()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R(2, 0, 6, 1));
        sheet.PrintRanges.Add(R(2, 0, 6, 1));
        workbook.DefineName(new DefinedName("Block", $"{sheet.Name}.$A$3:$B$7"));

        Assert.True(Shifts.KeepsBlocksWhole(workbook, 0, R(0, 0, 1, 1), CellShift.Down, insert: true));
        Shifts.Insert(engine, 0, R(0, 0, 1, 1), CellShift.Down);

        Assert.Equal(R(4, 0, 8, 1), sheet.AutoFilter.Range);
        Assert.Equal(R(4, 0, 8, 1), Assert.Single(sheet.PrintRanges));
        Assert.Equal($"{sheet.Name}.$A$5:$B$9", workbook.FindName("Block")!.Expression);
    }

    [Fact]
    public void Cells_shifted_left_move_a_database_range_and_drop_its_deleted_column_filter()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Db", SheetIndex = 0, Range = R(0, 2, 4, 4), Filters = [new ColumnFilter(0) { Criteria = "x" }, new ColumnFilter(2) { Criteria = "y" }] });

        Shifts.Delete(engine, 0, R(0, 0, 4, 0), CellShift.Right);

        var moved = workbook.DatabaseRanges.All.Single();
        Assert.Equal(R(0, 1, 4, 3), moved.Range);
        Assert.Equal([0, 2], moved.Filters.Select(filter => filter.ColumnOffset));
    }

    [Fact]
    public void A_shift_through_part_of_a_filtered_area_merge_or_array_is_refused()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R(4, 0, 8, 3));

        Assert.False(Shifts.KeepsBlocksWhole(workbook, 0, R(0, 0, 1, 1), CellShift.Down, insert: true));
        Assert.True(Shifts.KeepsBlocksWhole(workbook, 0, R(0, 0, 1, 3), CellShift.Down, insert: true));
        Assert.True(Shifts.KeepsBlocksWhole(workbook, 0, R(10, 0, 11, 1), CellShift.Down, insert: true));

        sheet.Merges.Add(R(0, 6, 0, 8));
        Assert.False(Shifts.KeepsBlocksWhole(workbook, 0, R(0, 7, 0, 7), CellShift.Down, insert: false));

        engine.SetArrayFormula(new SheetCell(0, A(20, 0)), R(20, 0, 21, 1), "={1;2|3;4}");
        Assert.False(Shifts.KeepsBlocksWhole(workbook, 0, R(20, 1, 21, 1), CellShift.Right, insert: true));
    }

    [Fact]
    public void Row_breaks_frozen_rows_query_bindings_scenarios_labels_and_sparklines_follow_rows()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.RowBreaks.Add(10);
        sheet.Frozen = new FrozenPanes(3, 0);
        sheet.QueryBindings.AddOrUpdate(new QueryBinding
        {
            Id = "q", DisplayName = "q", ConnectionId = "c", Query = new PulseOffice.Data.QueryDefinition { Sql = "select 1" }, Anchor = A(5, 0), Range = R(5, 0, 7, 1),
        });
        sheet.Scenarios.Add(new Scenario { Name = "s", Range = R(4, 0, 4, 0), Values = new Dictionary<CellAddress, CellValue> { [A(4, 0)] = CellValue.Number(1) } });
        sheet.Labels.Add(new LabelRange(R(3, 0, 3, 2), R(4, 0, 9, 2), LabelOrientation.Columns));
        sheet.Sparklines.Add(new SparklineGroup { Id = "g", Sparklines = [new Sparkline(A(5, 5), R(5, 0, 5, 3))] });

        _structure.InsertRows(engine, 0, 1, 2);

        Assert.Equal([12], sheet.RowBreaks);
        Assert.Equal(5, sheet.Frozen.Rows);
        Assert.Equal(R(7, 0, 9, 1), sheet.QueryBindings.Find("q")!.Range);
        Assert.Equal(A(7, 0), sheet.QueryBindings.Find("q")!.Anchor);
        Assert.Equal(R(6, 0, 6, 0), sheet.Scenarios.Single().Range);
        Assert.True(sheet.Scenarios.Single().Values.ContainsKey(A(6, 0)));
        Assert.Equal(R(5, 0, 5, 2), sheet.Labels.All.Single().Labels);
        var sparkline = sheet.Sparklines.Groups.Single().Sparklines.Single();
        Assert.Equal(A(7, 5), sparkline.Cell);
        Assert.Equal(R(7, 0, 7, 3), sparkline.Source);

        _structure.DeleteRows(engine, 0, 0, 2);
        Assert.Equal(3, sheet.Frozen.Rows);
    }

    [Fact]
    public void A_pivot_source_destination_and_fields_follow_the_columns_of_its_sheet()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var name = workbook.Sheets[0].Name;
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p", Name = "p", SourceSheet = name, SourceRange = R(0, 1, 9, 3), Destination = A(0, 6), DestinationSheet = name,
            Fields =
            [
                new PivotField { SourceName = "a", SourceColumnIndex = 0, Area = PivotArea.Row },
                new PivotField { SourceName = "b", SourceColumnIndex = 1, Area = PivotArea.Column },
                new PivotField { SourceName = "c", SourceColumnIndex = 2, Area = PivotArea.Value },
            ],
        });

        _structure.DeleteColumns(engine, 0, 2, 1);

        var pivot = workbook.Pivots.Single();
        Assert.Equal(R(0, 1, 9, 2), pivot.SourceRange);
        Assert.Equal(A(0, 5), pivot.Destination);
        Assert.Equal(["a", "c"], pivot.Fields.Select(field => field.SourceName));
        Assert.Equal([0, 1], pivot.Fields.Select(field => field.SourceColumnIndex));
    }

    [Fact]
    public void Conditions_on_another_sheet_follow_the_edited_sheet()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var name = workbook.Sheets[0].Name;
        var other = workbook.AddSheet("Other");
        other.Validations.Add(new DataValidation { Range = R(0, 0, 0, 0), Kind = ValidationKind.List, FirstFormula = $"{name}.$A$1:$A$5" });

        _structure.InsertRows(engine, 0, 0, 1);

        Assert.Equal($"{name}.$A$2:$A$6", other.Validations.Single().FirstFormula);
    }

    [Fact]
    public void The_workbook_checkpoint_restores_names_scenarios_and_frozen_panes()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        workbook.DefineName(new DefinedName("Data", $"{sheet.Name}.$B$2"));
        sheet.Frozen = new FrozenPanes(2, 0);
        var checkpoints = new WorkbookCheckpoints(new SheetSnapshots());
        var before = checkpoints.Capture(workbook);

        _structure.InsertRows(engine, 0, 0, 1);
        checkpoints.Restore(engine, workbook, before);

        Assert.Equal($"{sheet.Name}.$B$2", workbook.FindName("Data")!.Expression);
        Assert.Equal(2, sheet.Frozen.Rows);
    }
}
