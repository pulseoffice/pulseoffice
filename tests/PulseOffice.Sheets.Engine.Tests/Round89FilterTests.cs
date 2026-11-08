using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round89FilterTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly SheetFilterService _filter = new(new Evaluation.Functions.NumericText(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("it-IT"))));

    private static (Worksheet Sheet, CellRange Range) Numbers(params double[] values)
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Head"));
        for (var index = 0; index < values.Length; index++) sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Number(values[index]));
        return (sheet, new CellRange(CellAddress.First, new CellAddress(values.Length, 0)));
    }

    [Fact]
    public void A_canonical_decimal_condition_is_read_with_the_point_whatever_the_interface_language()
    {
        var (sheet, range) = Numbers(1, 1.4, 1.6, 2);

        var hidden = _filter.RowsToHide(sheet, range, [new ColumnFilter(0) { Criteria = ">1.5" }]);

        Assert.Equal([1, 2], hidden.Order());
    }

    [Fact]
    public void Two_conditions_joined_by_and_keep_the_rows_between()
    {
        var (sheet, range) = Numbers(1, 2, 3, 4, 5);

        var hidden = _filter.RowsToHide(sheet, range, [new ColumnFilter(0) { Criteria = ">=2", SecondCriteria = "<=4", Join = CriteriaJoin.And }]);

        Assert.Equal([1, 5], hidden.Order());
    }

    [Fact]
    public void Two_conditions_joined_by_or_keep_the_rows_outside()
    {
        var (sheet, range) = Numbers(1, 2, 3, 4, 5);

        var hidden = _filter.RowsToHide(sheet, range, [new ColumnFilter(0) { Criteria = "<2", SecondCriteria = ">4", Join = CriteriaJoin.Or }]);

        Assert.Equal([2, 3, 4], hidden.Order());
    }

    [Fact]
    public void A_filter_with_only_a_second_condition_is_not_empty()
    {
        Assert.False(new ColumnFilter(0) { SecondCriteria = ">1" }.IsEmpty);
    }

    [Theory]
    [InlineData("it-IT", "1,5", "1.5")]
    [InlineData("de-DE", "1.234,5", "1234.5")]
    [InlineData("en-US", "1,234.5", "1234.5")]
    [InlineData("it-IT", "abc", "abc")]
    public void Typed_values_are_stored_canonically(string culture, string typed, string canonical)
    {
        var values = new FilterValueText(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo(culture)));

        Assert.Equal(canonical, values.Canonical(typed));
    }

    [Fact]
    public void A_typed_date_becomes_its_serial_and_shows_again_as_a_date()
    {
        var values = new FilterValueText(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("it-IT")));

        var canonical = values.Canonical("05/01/2024");

        Assert.Equal("45296", canonical);
        Assert.Equal("05/01/2024", values.Display(canonical, dates: true));
        Assert.Equal("1,5", values.Display("1.5", dates: false));
    }

    [Fact]
    public void The_standard_filter_reads_a_localized_decimal_as_a_number()
    {
        var (sheet, range) = Numbers(1, 1.4, 1.6, 2);
        var standard = new StandardFilter(_filter, new FilterValueText(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("it-IT"))));

        var hidden = standard.RowsToHide(sheet, range, [new StandardFilterRow(FilterJoin.And, 0, FilterCondition.Greater, "1,5")]);

        Assert.Equal([1, 2], hidden.Order());
    }

    [Fact]
    public void Subtotal_ignores_rows_hidden_by_a_standard_or_advanced_filter()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 5; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 1)), "=SUBTOTAL(9;A1:A5)");

        sheet.Layout.HideRows(1, 2);
        Assert.Equal(15, engine.GetValue(new SheetCell(0, new CellAddress(0, 1))).AsNumber());

        sheet.Layout.MarkFilteredRows([1, 2]);

        Assert.Equal(10, engine.GetValue(new SheetCell(0, new CellAddress(0, 1))).AsNumber());
    }
}
