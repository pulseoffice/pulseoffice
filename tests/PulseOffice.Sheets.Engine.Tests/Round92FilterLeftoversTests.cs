using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round92FilterLeftoversTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly SheetStructure _structure = new();

    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    [Fact]
    public void The_autofilter_area_and_its_columns_follow_inserted_and_deleted_columns()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R(0, 1, 5, 4));
        sheet.AutoFilter.Set(new ColumnFilter(1) { Criteria = "North" });
        sheet.AutoFilter.Set(new ColumnFilter(3) { Criteria = ">15" });

        _structure.InsertColumns(engine, 0, 0, 1);
        Assert.Equal(R(0, 2, 5, 5), sheet.AutoFilter.Range);

        _structure.DeleteColumns(engine, 0, 3, 1);

        Assert.Equal(R(0, 2, 5, 4), sheet.AutoFilter.Range);
        var kept = Assert.Single(sheet.AutoFilter.Filters);
        Assert.Equal(2, kept.ColumnOffset);
        Assert.Equal(">15", kept.Criteria);
    }

    [Fact]
    public void Rows_inserted_above_move_the_autofilter_and_deleting_all_its_rows_removes_it()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R(2, 0, 6, 1));

        _structure.InsertRows(engine, 0, 0, 2);
        Assert.Equal(R(4, 0, 8, 1), sheet.AutoFilter.Range);

        _structure.DeleteRows(engine, 0, 3, 7);
        Assert.False(sheet.AutoFilter.IsActive);
    }

    [Fact]
    public void A_deleted_column_drops_its_terms_and_renumbers_the_tree()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.RangeFilter = new SheetRangeFilter(R(0, 0, 5, 2), true, new RangeFilter
        {
            Condition = new FilterGroup(FilterGroupJoin.Or,
            [
                new FilterGroup(FilterGroupJoin.And, [new FilterTerm(0, "North"), new FilterTerm(1, ">15")]),
                new FilterTerm(2, "East"),
            ]),
        });

        _structure.DeleteColumns(engine, 0, 0, 1);

        var moved = Assert.IsType<SheetRangeFilter>(sheet.RangeFilter);
        Assert.Equal(R(0, 0, 5, 1), moved.Range);
        Assert.Equal(new FilterGroup(FilterGroupJoin.Or, [new FilterTerm(0, ">15"), new FilterTerm(1, "East")]), moved.Filter.Condition);
    }

    [Fact]
    public void Named_range_filters_and_criteria_ranges_on_other_sheets_follow_the_edit()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.AddSheet("Criteria");
        var criteria = new AdvancedCriteria(1, R(0, 0, 1, 0));
        workbook.DatabaseRanges.Define(new DatabaseRange
        {
            Name = "Sales",
            SheetIndex = 0,
            Range = R(0, 0, 5, 2),
            Filters = [new ColumnFilter(2) { Criteria = "x" }],
            RangeFilter = new RangeFilter { Condition = new FilterTerm(2, ">1"), Criteria = criteria },
        });

        _structure.InsertColumns(engine, 0, 1, 1);
        var sales = workbook.DatabaseRanges.Find("Sales")!;
        Assert.Equal(R(0, 0, 5, 3), sales.Range);
        Assert.Equal(3, Assert.Single(sales.Filters).ColumnOffset);
        Assert.Equal(new FilterTerm(3, ">1"), sales.RangeFilter!.Condition);

        _structure.InsertRows(engine, 1, 0, 2);
        Assert.Equal(R(2, 0, 3, 0), workbook.DatabaseRanges.Find("Sales")!.RangeFilter!.Criteria!.Range);

        _structure.DeleteRows(engine, 1, 2, 2);
        var dropped = workbook.DatabaseRanges.Find("Sales")!.RangeFilter!;
        Assert.Null(dropped.Criteria);
        Assert.Equal(new FilterTerm(3, ">1"), dropped.Condition);
    }

    [Fact]
    public void The_sheet_snapshot_restores_filters_moved_by_a_structural_edit()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R(0, 0, 5, 1));
        sheet.AutoFilter.Set(new ColumnFilter(1) { Criteria = "North" });
        sheet.RangeFilter = new SheetRangeFilter(R(10, 0, 12, 1), true, new RangeFilter { Condition = new FilterTerm(1, "a") });
        var snapshots = new SheetSnapshots();
        var before = snapshots.Capture(sheet);

        _structure.DeleteColumns(engine, 0, 1, 1);
        Assert.Empty(sheet.AutoFilter.Filters);
        Assert.Null(sheet.RangeFilter);

        snapshots.Restore(engine, 0, before);

        Assert.Equal(R(0, 0, 5, 1), sheet.AutoFilter.Range);
        Assert.Equal("North", Assert.Single(sheet.AutoFilter.Filters).Criteria);
        Assert.Equal(new FilterTerm(1, "a"), sheet.RangeFilter!.Filter.Condition);
    }

    [Fact]
    public void A_left_to_right_tree_decomposes_back_into_dialog_rows()
    {
        var standard = new StandardFilter(new SheetFilterService());
        IReadOnlyList<StandardFilterRow> rows =
        [
            new(FilterJoin.And, 0, FilterCondition.Equal, "North"),
            new(FilterJoin.And, 1, FilterCondition.Greater, "15"),
            new(FilterJoin.Or, 0, FilterCondition.Contains, "ast"),
            new(FilterJoin.And, 1, FilterCondition.NotContains, "x"),
        ];

        var decomposed = standard.Decompose(standard.Compose(rows)!, 8);

        Assert.Equal(rows, decomposed);
    }

    [Fact]
    public void A_tree_the_dialog_cannot_show_is_reported_as_not_decomposable()
    {
        var standard = new StandardFilter(new SheetFilterService());
        var grouped = new FilterGroup(FilterGroupJoin.Or,
        [
            new FilterGroup(FilterGroupJoin.And, [new FilterTerm(0, "a"), new FilterTerm(1, "b")]),
            new FilterGroup(FilterGroupJoin.And, [new FilterTerm(0, "c"), new FilterTerm(1, "d")]),
        ]);
        var many = new FilterGroup(FilterGroupJoin.And, [.. Enumerable.Range(0, 9).Select(field => (FilterNode)new FilterTerm(field, "x"))]);

        Assert.Null(standard.Decompose(grouped, 8));
        Assert.Null(standard.Decompose(many, 8));
        Assert.Equal([new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Equal, "North")], standard.Decompose(new FilterTerm(0, "North"), 8));
    }
}
