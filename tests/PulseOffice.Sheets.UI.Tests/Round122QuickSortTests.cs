using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round122QuickSortTests
{
    private readonly QuickSortPlanner _planner = new(new SheetRegions(), new HeaderRowDetector());

    private static Workbook Table()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Base");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Name"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Score"));
        string[] names = ["c", "a", "b"];
        for (var row = 1; row <= 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(names[row - 1]));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row * 10));
        }
        return workbook;
    }

    [Fact]
    public void A_single_cell_extends_to_the_current_region_with_its_header_and_sorts_by_the_cursor_column()
    {
        var workbook = Table();
        var cursor = new CellAddress(2, 1);

        var plan = _planner.Plan(workbook, 0, new CellRange(cursor, cursor));

        Assert.NotNull(plan);
        Assert.Equal(new CellRange(CellAddress.First, new CellAddress(3, 1)), plan.Range);
        Assert.True(plan.HasHeader);
        Assert.Equal(1, plan.Column);
    }

    [Fact]
    public void Inside_an_autofilter_the_filtered_range_and_its_header_are_sorted()
    {
        var workbook = Table();
        var filtered = new CellRange(CellAddress.First, new CellAddress(3, 1));
        workbook.Sheets[0].AutoFilter.Activate(filtered);
        var cursor = new CellAddress(3, 0);

        var plan = _planner.Plan(workbook, 0, new CellRange(cursor, cursor));

        Assert.Equal(filtered, plan!.Range);
        Assert.True(plan.HasHeader);
        Assert.Equal(0, plan.Column);
        Assert.True(plan.ReappliesAutoFilter);
    }

    [Fact]
    public void Custom_sort_orders_come_from_the_spreadsheet_locale()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var orders = new LocaleSortOrders(new CultureSortOrders(), new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("it-IT")));

            Assert.Equal("lunedì", orders.Current()[1].Items[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
