using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round130NestedSubtotalsTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private (Workbook Workbook, CalculationEngine Engine) List()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        object[][] rows = [["Region", "City", "Amount"], ["North", "A", 10], ["North", "A", 20], ["North", "B", 5], ["South", "C", 7]];
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < 3; column++)
                sheet.SetValue(new CellAddress(row, column), rows[row][column] is string text
                    ? CellValue.Text(text)
                    : CellValue.Number(Convert.ToDouble(rows[row][column], CultureInfo.InvariantCulture)));
        return (workbook, engine);
    }

    private static SubtotalRequest Nested(CellRange range) => new(range, GroupColumn: 0, ValueColumns: [2])
    {
        ThenGroupColumns = [1],
        FunctionLabel = "Total",
        GrandTotalLabel = "Grand total",
        Outline = true,
    };

    private static double Number(Worksheet sheet, int row, int column) => sheet.GetValue(new CellAddress(row, column)).AsNumber();

    private static string Text(Worksheet sheet, int row, int column) => sheet.GetValue(new CellAddress(row, column)).AsText();

    [Fact]
    public void Two_group_columns_give_nested_totals_below_the_data()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Nested(R(0, 0, 4, 2)));
        engine.RecalculateAll();
        var sheet = workbook.Sheets[0];

        Assert.Equal("A - Total", Text(sheet, 3, 1));
        Assert.Equal(30, Number(sheet, 3, 2));
        Assert.Equal("B - Total", Text(sheet, 5, 1));
        Assert.Equal(5, Number(sheet, 5, 2));
        Assert.Equal("North - Total", Text(sheet, 6, 0));
        Assert.Equal(35, Number(sheet, 6, 2));
        Assert.Equal("C - Total", Text(sheet, 8, 1));
        Assert.Equal("South - Total", Text(sheet, 9, 0));
        Assert.Equal(7, Number(sheet, 9, 2));
        Assert.Equal("Grand total", Text(sheet, 10, 0));
        Assert.Equal(42, Number(sheet, 10, 2));
    }

    [Fact]
    public void Nested_totals_get_one_outline_level_per_group_column()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Nested(R(0, 0, 4, 2)));
        var groups = workbook.Sheets[0].RowGroups;

        Assert.Equal(6, groups.Count);
        Assert.Contains(groups, group => group is { First: 1, Last: 9, Level: 1 });
        Assert.Contains(groups, group => group is { First: 1, Last: 5, Level: 2 });
        Assert.Contains(groups, group => group is { First: 7, Last: 8, Level: 2 });
        Assert.Contains(groups, group => group is { First: 1, Last: 2, Level: 3 });
        Assert.Contains(groups, group => group is { First: 4, Last: 4, Level: 3 });
        Assert.Contains(groups, group => group is { First: 7, Last: 7, Level: 3 });
    }

    [Fact]
    public void Nested_totals_above_the_data_come_before_their_rows()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Nested(R(0, 0, 4, 2)) with { SummaryBelow = false });
        engine.RecalculateAll();
        var sheet = workbook.Sheets[0];

        Assert.Equal("Grand total", Text(sheet, 1, 0));
        Assert.Equal(42, Number(sheet, 1, 2));
        Assert.Equal("North - Total", Text(sheet, 2, 0));
        Assert.Equal(35, Number(sheet, 2, 2));
        Assert.Equal("A - Total", Text(sheet, 3, 1));
        Assert.Equal(30, Number(sheet, 3, 2));
        Assert.Equal("North", Text(sheet, 4, 0));
        Assert.Equal("B - Total", Text(sheet, 6, 1));
        Assert.Equal("South - Total", Text(sheet, 8, 0));
        Assert.Equal("C - Total", Text(sheet, 9, 1));
        Assert.Equal(7, Number(sheet, 10, 2));
        Assert.False(sheet.OutlineSummary.Below);
        Assert.Contains(sheet.RowGroups, group => group is { First: 2, Last: 10, Level: 1 });
        Assert.Contains(sheet.RowGroups, group => group is { First: 3, Last: 7, Level: 2 });
        Assert.Contains(sheet.RowGroups, group => group is { First: 4, Last: 5, Level: 3 });
    }

    [Fact]
    public void Replacing_removes_every_nested_level()
    {
        var (workbook, engine) = List();
        var subtotals = new SheetSubtotals();
        subtotals.Apply(engine, 0, Nested(R(0, 0, 4, 2)));

        var outcome = subtotals.Apply(engine, 0, Nested(R(0, 0, 10, 2)) with { ThenGroupColumns = [], ReplaceCurrent = true });
        engine.RecalculateAll();
        var sheet = workbook.Sheets[0];

        Assert.Equal(new SubtotalOutcome(6, 3), outcome);
        Assert.Equal("North - Total", Text(sheet, 4, 0));
        Assert.Equal("Grand total", Text(sheet, 7, 0));
        Assert.True(sheet.GetValue(new CellAddress(8, 0)).IsEmpty);
        Assert.Equal(3, sheet.RowGroups.Count);
    }

    [Fact]
    public void A_page_break_separates_only_the_outer_groups()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Nested(R(0, 0, 4, 2)) with { PageBreaks = true });

        Assert.Equal([7], workbook.Sheets[0].RowBreaks);
    }

    [Fact]
    public void Summary_position_is_restored_with_the_sheet_snapshot()
    {
        var (workbook, engine) = List();
        var snapshots = new SheetSnapshots();
        var before = snapshots.Capture(workbook.Sheets[0]);
        new SheetSubtotals().Apply(engine, 0, Nested(R(0, 0, 4, 2)) with { SummaryBelow = false });

        snapshots.Restore(engine, 0, before);

        Assert.Equal(OutlineSummary.Default, workbook.Sheets[0].OutlineSummary);
        Assert.Empty(workbook.Sheets[0].RowGroups);
    }
}
