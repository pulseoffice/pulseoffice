using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round129SubtotalsTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private (Workbook Workbook, CalculationEngine Engine) List()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        object[][] rows = [["Region", "Amount", "Units"], ["North", 10, 1], ["North", 20, 2], ["South", 5, 3]];
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < 3; column++)
                sheet.SetValue(new CellAddress(row, column), rows[row][column] is string text
                    ? CellValue.Text(text)
                    : CellValue.Number(Convert.ToDouble(rows[row][column], CultureInfo.InvariantCulture)));
        return (workbook, engine);
    }

    private static SubtotalRequest Request(CellRange range) => new(range, GroupColumn: 0, ValueColumns: [1, 2])
    {
        ColumnFunctions = new Dictionary<int, SubtotalFunction> { [2] = SubtotalFunction.Max },
        FunctionLabel = "Total",
        GrandTotalLabel = "Grand total",
    };

    [Fact]
    public void Several_value_columns_each_get_their_own_function()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Request(R(0, 0, 3, 2)));
        engine.RecalculateAll();
        var sheet = workbook.Sheets[0];

        Assert.Equal(30, sheet.GetValue(new CellAddress(3, 1)).AsNumber());
        Assert.Equal(2, sheet.GetValue(new CellAddress(3, 2)).AsNumber());
        Assert.Equal(35, sheet.GetValue(new CellAddress(6, 1)).AsNumber());
        Assert.Equal(3, sheet.GetValue(new CellAddress(6, 2)).AsNumber());
    }

    [Fact]
    public void Replacing_current_subtotals_removes_the_old_rows_and_groups_first()
    {
        var (workbook, engine) = List();
        var subtotals = new SheetSubtotals();
        subtotals.Apply(engine, 0, Request(R(0, 0, 3, 2)) with { Outline = true });

        var outcome = subtotals.Apply(engine, 0, Request(R(0, 0, 6, 2)) with { ReplaceCurrent = true, Outline = true });
        engine.RecalculateAll();
        var sheet = workbook.Sheets[0];

        Assert.Equal(new SubtotalOutcome(3, 3), outcome);
        Assert.Equal("Grand total", sheet.GetValue(new CellAddress(6, 0)).AsText());
        Assert.True(sheet.GetValue(new CellAddress(7, 0)).IsEmpty);
        Assert.Equal(3, sheet.RowGroups.Count);
    }

    [Fact]
    public void Outline_groups_wrap_each_group_and_the_whole_list()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Request(R(0, 0, 3, 2)) with { Outline = true });
        var groups = workbook.Sheets[0].RowGroups;

        Assert.Contains(groups, group => group is { First: 1, Last: 5, Level: 1 });
        Assert.Contains(groups, group => group is { First: 1, Last: 2, Level: 2 });
        Assert.Contains(groups, group => group is { First: 4, Last: 4, Level: 2 });
    }

    [Fact]
    public void A_page_break_separates_the_groups()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Request(R(0, 0, 3, 2)) with { PageBreaks = true });

        Assert.Equal([4], workbook.Sheets[0].RowBreaks);
    }

    [Fact]
    public void Summary_above_the_data_puts_the_totals_before_each_group()
    {
        var (workbook, engine) = List();
        new SheetSubtotals().Apply(engine, 0, Request(R(0, 0, 3, 2)) with { SummaryBelow = false, Outline = true });
        engine.RecalculateAll();
        var sheet = workbook.Sheets[0];

        Assert.Equal("Grand total", sheet.GetValue(new CellAddress(1, 0)).AsText());
        Assert.Equal(35, sheet.GetValue(new CellAddress(1, 1)).AsNumber());
        Assert.Equal("North - Total", sheet.GetValue(new CellAddress(2, 0)).AsText());
        Assert.Equal(30, sheet.GetValue(new CellAddress(2, 1)).AsNumber());
        Assert.Equal("North", sheet.GetValue(new CellAddress(3, 0)).AsText());
        Assert.Equal("South - Total", sheet.GetValue(new CellAddress(5, 0)).AsText());
        Assert.Equal(5, sheet.GetValue(new CellAddress(5, 1)).AsNumber());
        Assert.Contains(sheet.RowGroups, group => group is { First: 2, Last: 6, Level: 1 });
        Assert.Contains(sheet.RowGroups, group => group is { First: 3, Last: 4, Level: 2 });
    }

    [Fact]
    public void Moving_a_sorted_range_carries_its_remembered_sort()
    {
        var (workbook, engine) = List();
        var sheet = workbook.Sheets[0];
        sheet.RangeSort = new SheetRangeSort(R(0, 0, 3, 2), true, new SortSettings { Fields = [new SortField(1)] });

        new SheetRangeMove(new SheetStructure()).Move(engine, new RangeMove(sheet.Name, R(0, 0, 3, 2), sheet.Name, new CellAddress(10, 4)));

        Assert.Equal(R(10, 4, 13, 6), sheet.RangeSort?.Range);
    }

    [Fact]
    public void Overwriting_a_sorted_range_by_a_move_drops_its_remembered_sort()
    {
        var (workbook, engine) = List();
        var sheet = workbook.Sheets[0];
        sheet.RangeSort = new SheetRangeSort(R(10, 4, 11, 5), false, new SortSettings { Fields = [new SortField(0)] });

        new SheetRangeMove(new SheetStructure()).Move(engine, new RangeMove(sheet.Name, R(0, 0, 3, 2), sheet.Name, new CellAddress(10, 4)));

        Assert.Null(sheet.RangeSort);
    }

    [Fact]
    public void Consolidation_settings_follow_sheet_removal_and_come_back_with_the_sheet()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Two");
        workbook.AddSheet("Three");
        var settings = new ConsolidationSettings
        {
            Sources = [new ConsolidationArea(1, R(0, 0, 3, 1)), new ConsolidationArea(2, R(0, 0, 3, 1))],
            TargetSheet = 2,
            Target = new CellAddress(5, 5),
        };
        workbook.Consolidation = settings;
        var two = workbook.Sheets[1];

        workbook.RemoveSheet(two);
        Assert.Equal([new ConsolidationArea(1, R(0, 0, 3, 1))], workbook.Consolidation!.Sources);
        Assert.Equal(1, workbook.Consolidation.TargetSheet);

        workbook.InsertSheet(two, 1);
        Assert.Same(settings, workbook.Consolidation);
    }

    [Fact]
    public void Consolidation_settings_follow_inserted_rows()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Consolidation = new ConsolidationSettings { Sources = [new ConsolidationArea(0, R(2, 0, 4, 1))], Target = new CellAddress(8, 0) };

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Equal(R(4, 0, 6, 1), workbook.Consolidation!.Sources[0].Range);
        Assert.Equal(new CellAddress(10, 0), workbook.Consolidation.Target);
    }
}
