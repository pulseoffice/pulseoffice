using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round87FilterTests
{
    private readonly SheetFilterService _filter = new();

    private static (Worksheet Sheet, CellRange Range) Column(params string?[] values)
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Head"));
        for (var index = 0; index < values.Length; index++)
            if (values[index] is { } text) sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Text(text));
        return (sheet, new CellRange(CellAddress.First, new CellAddress(values.Length, 0)));
    }

    [Fact]
    public void A_value_filter_admitting_nothing_hides_every_row()
    {
        var (sheet, range) = Column("a", null, "b");

        var hidden = _filter.RowsToHide(sheet, range, [new ColumnFilter(0) { AllowedValues = new HashSet<string>(), IncludeBlanks = false }]);

        Assert.Equal([1, 2, 3], hidden.Order());
    }

    [Fact]
    public void A_value_filter_admitting_only_blanks_shows_only_blank_rows()
    {
        var (sheet, range) = Column("a", null, "b");

        var hidden = _filter.RowsToHide(sheet, range, [new ColumnFilter(0) { AllowedValues = new HashSet<string>(), IncludeBlanks = true }]);

        Assert.Equal([1, 3], hidden.Order());
    }

    [Fact]
    public void All_values_without_blanks_is_a_filter_that_hides_blank_rows()
    {
        var (sheet, range) = Column("a", null, "b");
        var filter = new ColumnFilter(0) { IncludeBlanks = false };

        Assert.False(filter.IsEmpty);
        Assert.Equal([2], _filter.RowsToHide(sheet, range, [filter]));
    }

    [Theory]
    [InlineData("*a*")]
    [InlineData("<5")]
    [InlineData("<>")]
    public void A_condition_judges_blank_rows_instead_of_letting_them_through(string criteria)
    {
        var (sheet, range) = Column("a", null);
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(1, 0), criteria == "*a*" ? CellValue.Text("cat") : CellValue.Number(1));

        var hidden = _filter.RowsToHide(sheet, range, [new ColumnFilter(0) { Criteria = criteria }]);

        Assert.Equal([2], hidden);
    }

    [Fact]
    public void Not_equal_still_shows_blank_rows()
    {
        var (sheet, range) = Column("a", null, "b");

        Assert.Equal([1], _filter.RowsToHide(sheet, range, [new ColumnFilter(0) { Criteria = "<>a" }]));
    }

    [Fact]
    public void Standard_filter_contains_no_longer_shows_blank_rows()
    {
        var (sheet, range) = Column("alpha", null, "beta");

        var hidden = new StandardFilter(_filter).RowsToHide(sheet, range, [new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Contains, "al")]);

        Assert.Equal([2, 3], hidden.Order());
    }

    [Fact]
    public void Duplicate_condition_filters_do_not_break_the_evaluation()
    {
        var (sheet, range) = Column("a", "b");
        var twice = new ColumnFilter(0) { Criteria = "a" };

        Assert.Equal([2], _filter.RowsToHide(sheet, range, [twice, twice]));
    }
}
