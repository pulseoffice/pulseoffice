using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round90FilterModelTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly SheetFilterService _filter = new();

    private static CellAddress A(string reference) => CellAddress.TryParseA1(reference, out var address) ? address : throw new ArgumentException(reference);

    private static void Table(Worksheet sheet)
    {
        sheet.SetValue(A("A1"), CellValue.Text("Region"));
        sheet.SetValue(A("B1"), CellValue.Text("Amount"));
        string[] regions = ["North", "South", "North", "East", "South"];
        double[] amounts = [10, 20, 30, 40, 50];
        for (var index = 0; index < regions.Length; index++)
        {
            sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Text(regions[index]));
            sheet.SetValue(new CellAddress(index + 1, 1), CellValue.Number(amounts[index]));
        }
    }

    private static readonly CellRange Data = new(CellAddress.First, new CellAddress(5, 1));

    [Fact]
    public void A_condition_tree_mixes_fields_inside_or_groups()
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        Table(sheet);
        var tree = new FilterGroup(FilterGroupJoin.Or,
        [
            new FilterGroup(FilterGroupJoin.And, [new FilterTerm(0, "North"), new FilterTerm(1, ">15")]),
            new FilterTerm(0, "East"),
        ]);

        var hidden = _filter.RowsToHide(sheet, Data, tree);

        Assert.Equal([1, 2, 5], hidden.Order());
    }

    [Fact]
    public void The_standard_filter_composes_its_rows_left_to_right()
    {
        var standard = new StandardFilter(_filter);
        var tree = standard.Compose(
        [
            new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Equal, "North"),
            new StandardFilterRow(FilterJoin.And, 1, FilterCondition.Greater, "15"),
            new StandardFilterRow(FilterJoin.Or, 0, FilterCondition.Equal, "East"),
        ]);

        var or = Assert.IsType<FilterGroup>(tree);
        Assert.Equal(FilterGroupJoin.Or, or.Join);
        Assert.Equal(FilterGroupJoin.And, Assert.IsType<FilterGroup>(or.Children[0]).Join);
        Assert.Equal(new FilterTerm(0, "=East"), or.Children[1]);
    }

    [Fact]
    public void Computed_criteria_are_evaluated_relative_to_the_first_data_row()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        Table(sheet);
        sheet.SetValue(A("D1"), CellValue.Text("Above average"));
        engine.SetFormula(new SheetCell(0, A("D2")), "=B2>AVERAGE($B$2:$B$6)");

        var hidden = _filter.RowsToHide(sheet, new AdvancedFilterRequest(Data, new CellRange(A("D1"), A("D2")))
        {
            Formulas = new EngineShiftedFormulas(engine),
        });

        Assert.Equal([1, 2, 3], hidden.Order());
    }

    [Fact]
    public void Computed_criteria_under_a_blank_header_combine_with_matched_columns()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        Table(sheet);
        sheet.SetValue(A("D1"), CellValue.Text("Region"));
        sheet.SetValue(A("D2"), CellValue.Text("South"));
        engine.SetFormula(new SheetCell(0, A("E2")), "=B2>25");

        var hidden = _filter.RowsToHide(sheet, new AdvancedFilterRequest(Data, new CellRange(A("D1"), A("E2")))
        {
            Formulas = new EngineShiftedFormulas(engine),
        });

        Assert.Equal([1, 2, 3, 4], hidden.Order());
    }

    [Fact]
    public void Stored_filters_of_named_ranges_are_applied_on_restore()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        Table(sheet);
        workbook.DatabaseRanges.Define(new DatabaseRange
        {
            Name = "Sales",
            SheetIndex = 0,
            Range = Data,
            Filters = [new ColumnFilter(0) { Criteria = "North" }],
        });

        new RangeFilterEvaluator(_filter).Restore(workbook, new EngineShiftedFormulas(engine));

        Assert.Equal([2, 4, 5], sheet.Layout.RowsHiddenByFilter.Order());
        Assert.True(sheet.Layout.IsRowFiltered(2));
    }

    [Fact]
    public void A_stored_advanced_filter_with_unique_records_hides_duplicates()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        Table(sheet);
        sheet.SetValue(new CellAddress(3, 1), CellValue.Number(10));
        sheet.SetValue(A("D1"), CellValue.Text("Region"));
        sheet.SetValue(A("D2"), CellValue.Text("North"));
        var filter = new RangeFilter { Criteria = new AdvancedCriteria(0, new CellRange(A("D1"), A("D2"))), UniqueOnly = true };

        var hidden = new RangeFilterEvaluator(_filter).RowsToHide(workbook, sheet, Data, true, filter);

        Assert.Equal([2, 3, 4, 5], hidden.Order());
    }

    [Fact]
    public void Filtered_row_marks_follow_row_insertion_and_deletion()
    {
        var layout = new SheetLayout();
        layout.ApplyFilteredRows([3, 5]);

        layout.InsertRows(1, 2);
        Assert.Equal([5, 7], layout.RowsHiddenByFilter.Order());
        Assert.True(layout.IsRowFiltered(5));
        Assert.False(layout.IsRowFiltered(3));

        layout.DeleteRows(0, 1);
        Assert.True(layout.IsRowFiltered(4));
        Assert.True(layout.IsRowFiltered(6));

        layout.ApplyFilteredRows(null);
        Assert.False(layout.IsRowHidden(4));
        Assert.False(layout.IsRowHidden(6));
    }

    [Fact]
    public void Removing_a_criteria_sheet_drops_the_criteria_and_moving_it_follows()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Criteria");
        workbook.AddSheet("Third");
        var sheet = workbook.Sheets[0];
        sheet.RangeFilter = new SheetRangeFilter(Data, true, new RangeFilter { Criteria = new AdvancedCriteria(1, new CellRange(A("A1"), A("A2"))) });

        workbook.MoveSheet(1, 2);
        Assert.Equal(2, sheet.RangeFilter!.Filter.Criteria!.SheetIndex);

        workbook.RemoveSheet(workbook.Sheets[2]);
        Assert.Null(sheet.RangeFilter);
    }
}
