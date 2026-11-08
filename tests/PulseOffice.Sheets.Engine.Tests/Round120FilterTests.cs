using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round120FilterTests
{
    private static Worksheet Sheet(params CellValue[] values)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("H"));
        for (var index = 0; index < values.Length; index++) sheet.SetValue(new CellAddress(index + 1, 0), values[index]);
        return sheet;
    }

    [Fact]
    public void Equal_to_empty_and_not_empty_survive_decomposition_and_hide_the_right_rows()
    {
        var standard = new StandardFilter(new SheetFilterService());

        Assert.Equal([new StandardFilterRow(FilterJoin.And, 0, FilterCondition.IsEmpty, string.Empty)], standard.Decompose(new FilterTerm(0, "="), 8));
        Assert.Equal([new StandardFilterRow(FilterJoin.And, 0, FilterCondition.IsNotEmpty, string.Empty)], standard.Decompose(new FilterTerm(0, "<>"), 8));
        Assert.True(standard.Applies(new StandardFilterRow(FilterJoin.And, 0, FilterCondition.IsEmpty, string.Empty)));
        Assert.False(standard.Applies(new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Equal, " ")));

        var sheet = Sheet(CellValue.Text("a"), CellValue.Empty, CellValue.Number(3));
        var range = new CellRange(CellAddress.First, new CellAddress(3, 0));
        Assert.Equal([1, 3], standard.RowsToHide(sheet, range, [new StandardFilterRow(FilterJoin.And, 0, FilterCondition.IsEmpty, string.Empty)]).Order());
        Assert.Equal([2], standard.RowsToHide(sheet, range, [new StandardFilterRow(FilterJoin.And, 0, FilterCondition.IsNotEmpty, string.Empty)]));
    }

    [Fact]
    public void Number_one_and_text_one_are_not_duplicates()
    {
        var sheet = Sheet(CellValue.Number(1), CellValue.Text("1"), CellValue.Number(1));
        var range = new CellRange(CellAddress.First, new CellAddress(3, 0));
        var workbook = Workbook.CreateDefault();

        Assert.Equal([3], new RangeFilterEvaluator(new SheetFilterService()).RowsToHide(workbook, sheet, range, true, new RangeFilter { UniqueOnly = true }));

        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("H"));
        Assert.Equal([3], new SheetFilterService().RowsToHide(sheet, new AdvancedFilterRequest(range, new CellRange(new CellAddress(0, 2), new CellAddress(1, 2))) { UniqueOnly = true }));
    }
}

public sealed class Round120SortTests
{
    [Fact]
    public void Sorting_rows_that_hold_part_of_an_array_formula_is_refused()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(3));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Number(2));
        sheet.SetArrayFormula(new CellRange(new CellAddress(0, 1), new CellAddress(1, 1)), "=A1:A2*2");
        var range = new CellRange(CellAddress.First, new CellAddress(2, 1));
        var sorter = new SheetSorter();

        sorter.Sort(sheet, range, [new SortKey(0)]);

        Assert.Equal(3, sheet[new CellAddress(0, 0)].Value.AsNumber());
        Assert.True(sheet[new CellAddress(0, 1)].IsArrayAnchor);
    }
}
