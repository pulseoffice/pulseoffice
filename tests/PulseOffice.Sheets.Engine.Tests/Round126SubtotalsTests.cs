using System.Diagnostics;
using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round126SubtotalsTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void The_grand_total_lands_below_the_last_group_subtotal_with_labels_in_the_group_column()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        object[][] rows = [["Amount", "Region"], [10, "North"], [20, "North"], [5, "South"]];
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < 2; column++)
                sheet.SetValue(new CellAddress(row, column), rows[row][column] is string text ? CellValue.Text(text) : CellValue.Number(Convert.ToDouble(rows[row][column], CultureInfo.InvariantCulture)));
        var subtotals = new SheetSubtotals();
        subtotals.Apply(engine, 0, new SubtotalRequest(new CellRange(CellAddress.First, new CellAddress(3, 1)), GroupColumn: 1, ValueColumns: [0])
        {
            FunctionLabel = "Sum",
            GrandTotalLabel = "Grand total",
        });
        engine.RecalculateAll();

        Assert.Equal("North - Sum", sheet.GetValue(new CellAddress(3, 1)).AsText());
        Assert.Equal(30, sheet.GetValue(new CellAddress(3, 0)).AsNumber());
        Assert.Equal("South - Sum", sheet.GetValue(new CellAddress(5, 1)).AsText());
        Assert.Equal("Grand total", sheet.GetValue(new CellAddress(6, 1)).AsText());
        Assert.Equal(35, sheet.GetValue(new CellAddress(6, 0)).AsNumber());
    }

    [Fact]
    public void Group_keys_are_compared_in_the_spreadsheet_locale_not_the_thread_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var sheet = Workbook.CreateDefault().Sheets[0];
            sheet.SetValue(new CellAddress(0, 0), CellValue.Text("INK"));
            sheet.SetValue(new CellAddress(1, 0), CellValue.Text("ink"));
            sheet.SetValue(new CellAddress(0, 1), CellValue.Number(1));
            sheet.SetValue(new CellAddress(1, 1), CellValue.Number(2));

            var plan = new SheetSubtotals(new SheetStructure(), new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("en-US"))).Plan(sheet,
                new SubtotalRequest(new CellRange(CellAddress.First, new CellAddress(1, 1)), GroupColumn: 0, ValueColumns: [1]) { HasHeader = false, GrandTotal = false });

            Assert.Single(plan.Rows);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Consolidation_labels_are_matched_in_the_spreadsheet_locale()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var workbook = Workbook.CreateDefault();
            var sheet = workbook.Sheets[0];
            sheet.SetValue(new CellAddress(0, 0), CellValue.Text("INK"));
            sheet.SetValue(new CellAddress(0, 1), CellValue.Number(1));
            sheet.SetValue(new CellAddress(1, 0), CellValue.Text("ink"));
            sheet.SetValue(new CellAddress(1, 1), CellValue.Number(2));

            var plan = new SheetConsolidation(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("en-US"))).Plan(workbook,
                new ConsolidationRequest([new ConsolidationSource(0, new CellRange(CellAddress.First, new CellAddress(1, 1)))], new CellAddress(5, 0))
                { LabelsInFirstColumn = true });

            Assert.Equal(3, plan.Values[new CellAddress(5, 1)].AsNumber());
            Assert.False(plan.Values.ContainsKey(new CellAddress(6, 1)));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Consolidating_whole_columns_reads_only_the_used_cells()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(2, 1), CellValue.Number(2));
        var whole = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 199));
        var clock = Stopwatch.StartNew();

        var plan = new SheetConsolidation().Plan(workbook,
            new ConsolidationRequest([new ConsolidationSource(0, whole), new ConsolidationSource(0, whole)], new CellAddress(0, 300)));

        clock.Stop();
        Assert.Equal(2, plan.Values[new CellAddress(0, 300)].AsNumber());
        Assert.Equal(4, plan.Values[new CellAddress(2, 301)].AsNumber());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"took {clock.Elapsed}");
    }
}
