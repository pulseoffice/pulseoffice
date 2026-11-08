using PulseOffice.Document.Forms;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round100MoveCopyTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly SheetRangeMove _moves = new(new SheetStructure());

    private readonly SheetDuplicator _duplicator = new();

    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private static CellAddress A(int row, int column) => new(row, column);

    [Fact]
    public void A_copied_sheet_duplicates_its_tables_with_unique_names_and_renamed_formulas_in_one_undoable_insert()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var source = workbook.Sheets[0];
        workbook.DatabaseRanges.Define(new DatabaseRange
        {
            Name = "Table1", SheetIndex = 0, Range = R(0, 0, 4, 1), HasTotalsRow = true,
            Filters = [new ColumnFilter(1) { Criteria = ">0" }],
            RangeFilter = new RangeFilter { Criteria = new AdvancedCriteria(0, R(10, 0, 11, 1)) },
        });
        engine.SetValue(new SheetCell(0, A(1, 0)), CellValue.Number(4));
        engine.SetFormula(new SheetCell(0, A(0, 5)), "=ROWS(Table1)");

        var copy = _duplicator.Duplicate(workbook, source, "Copy");
        _duplicator.Insert(workbook, copy, 0);

        var table = workbook.DatabaseRanges.Find("Table1_2");
        Assert.NotNull(table);
        Assert.Equal(0, table!.SheetIndex);
        Assert.True(table.HasTotalsRow);
        Assert.Single(table.Filters);
        Assert.Equal(new AdvancedCriteria(0, R(10, 0, 11, 1)), table.RangeFilter!.Criteria);
        Assert.Equal(1, workbook.DatabaseRanges.Find("Table1")!.SheetIndex);
        Assert.Equal(1, workbook.DatabaseRanges.Find("Table1")!.RangeFilter!.Criteria!.SheetIndex);
        Assert.Contains("Table1_2", copy.Sheet[A(0, 5)].Formula, StringComparison.Ordinal);
        Assert.DoesNotContain("Table1_2", source[A(0, 5)].Formula, StringComparison.Ordinal);

        _duplicator.Remove(workbook, copy);
        Assert.Null(workbook.DatabaseRanges.Find("Table1_2"));
        Assert.Equal(0, workbook.DatabaseRanges.Find("Table1")!.SheetIndex);

        _duplicator.Insert(workbook, copy, 0);
        Assert.Equal(0, workbook.DatabaseRanges.Find("Table1_2")!.SheetIndex);
        Assert.Equal(2, workbook.DatabaseRanges.Count);
    }

    [Fact]
    public void A_filter_or_table_wholly_inside_the_moved_range_moves_with_it_and_a_cut_one_still_refuses()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R(0, 0, 4, 1));
        sheet.AutoFilter.Set(new ColumnFilter(1) { Criteria = ">0" });
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "T", SheetIndex = 0, Range = R(0, 0, 4, 1) });
        var cut = new RangeMove(sheet.Name, R(0, 0, 2, 1), sheet.Name, A(20, 0));
        var whole = new RangeMove(sheet.Name, R(0, 0, 4, 2), sheet.Name, A(20, 3));
        Assert.False(_moves.KeepsBlocksWhole(workbook, cut));
        Assert.True(_moves.KeepsBlocksWhole(workbook, whole));

        _moves.Move(engine, whole);

        Assert.Equal(R(20, 3, 24, 4), sheet.AutoFilter.Range);
        Assert.Single(sheet.AutoFilter.Filters);
        Assert.Equal(R(20, 3, 24, 4), workbook.DatabaseRanges.Find("T")!.Range);
    }

    [Fact]
    public void A_table_moved_to_another_sheet_changes_sheet_and_a_landing_on_another_table_is_refused()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        var other = workbook.AddSheet("Two");
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "T", SheetIndex = 0, Range = R(0, 0, 2, 1) });
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "U", SheetIndex = 1, Range = R(5, 5, 8, 6) });

        Assert.False(_moves.KeepsBlocksWhole(workbook, new RangeMove(sheet.Name, R(0, 0, 2, 1), other.Name, A(4, 4))));

        var move = new RangeMove(sheet.Name, R(0, 0, 2, 1), other.Name, A(0, 0));
        Assert.True(_moves.KeepsBlocksWhole(workbook, move));
        _moves.Move(engine, move);

        Assert.Equal(1, workbook.DatabaseRanges.Find("T")!.SheetIndex);
    }

    [Fact]
    public void Rules_and_validations_partly_inside_the_moved_range_are_split_with_reanchored_formulas()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R(0, 0, 5, 0), Formula = "B1>0" });
        sheet.Validations.Add(new DataValidation { Range = R(0, 0, 5, 0), FirstFormula = "B1" });

        _moves.Move(engine, new RangeMove(sheet.Name, R(3, 0, 5, 0), sheet.Name, A(3, 4)));

        Assert.Equal(2, sheet.ConditionalRules.Count);
        var kept = sheet.ConditionalRules.Single(rule => rule.Range == R(0, 0, 2, 0));
        Assert.Equal("B1>0", kept.Formula);
        var moved = sheet.ConditionalRules.Single(rule => rule.Range == R(3, 4, 5, 4));
        Assert.Equal("B4>0", moved.Formula);

        Assert.Equal(2, sheet.Validations.Count);
        Assert.Equal("B1", sheet.Validations.Single(validation => validation.Range == R(0, 0, 2, 0)).FirstFormula);
        Assert.Equal("B4", sheet.Validations.Single(validation => validation.Range == R(3, 4, 5, 4)).FirstFormula);
    }

    [Fact]
    public void Tracked_changes_follow_a_move_and_the_move_itself_is_recorded()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        var other = workbook.AddSheet("Two");
        workbook.Changes.IsRecording = true;
        engine.SetValue(new SheetCell(0, A(0, 0)), CellValue.Number(5));
        var earlier = Assert.Single(workbook.Changes.Changes);

        _moves.Move(engine, new RangeMove(sheet.Name, R(0, 0, 0, 0), other.Name, A(2, 2)));

        var relocated = workbook.Changes.Changes.Single(change => change.Id == earlier.Id);
        Assert.Equal(1, relocated.SheetIndex);
        Assert.Equal(A(2, 2), relocated.Address);
        Assert.Contains(workbook.Changes.Changes, change => change.SheetIndex == 0 && change.Address == A(0, 0) && change.After.IsEmpty);
        Assert.Equal(CellValue.Number(5), workbook.Changes.At(1, A(2, 2))!.After);
    }

    [Fact]
    public void Form_controls_carried_to_another_sheet_qualify_their_links_with_the_source_sheet()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        var other = workbook.AddSheet("Two");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "box",
            Anchor = A(0, 0),
            Content = new FormControlObject
            {
                Id = "box",
                ControlKind = FormControlKind.ListBox,
                Properties = new FormControlProperties { LinkedCell = "H1", SourceRange = "A1:A3" },
            },
        });

        _moves.Move(engine, new RangeMove(sheet.Name, R(0, 0, 2, 0), other.Name, A(0, 0)));

        var properties = ((FormControlObject)Assert.Single(other.Objects).Content).Properties;
        Assert.Equal($"{sheet.Name}!H1", properties.LinkedCell);
        Assert.Equal("A1:A3", properties.SourceRange);
    }
}
