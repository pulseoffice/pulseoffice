using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;
using SortKey = PulseOffice.Sheets.Engine.Operations.SortKey;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round126SortTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void A_left_to_right_sort_reorders_columns_and_keeps_the_header_column()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Key"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Name"));
        double[] keys = [3, 1, 2];
        for (var index = 0; index < keys.Length; index++)
        {
            sheet.SetValue(new CellAddress(0, index + 1), CellValue.Number(keys[index]));
            sheet.SetValue(new CellAddress(1, index + 1), CellValue.Text("c" + keys[index]));
        }
        sheet.Comments[new CellAddress(1, 1)] = new CellComment { Text = "on three" };
        var history = new UndoStack();

        new SheetSorter().Sort(sheet, new CellRange(CellAddress.First, new CellAddress(1, 3)), [new SortKey(0)], hasHeader: true,
            history: history, orientation: SortOrientation.LeftToRight);

        Assert.Equal("Key", sheet.GetValue(CellAddress.First).AsText());
        Assert.Equal([1d, 2d, 3d], [.. Enumerable.Range(1, 3).Select(column => sheet.GetValue(new CellAddress(0, column)).AsNumber())]);
        Assert.Equal("c3", sheet.GetValue(new CellAddress(1, 3)).AsText());
        Assert.True(sheet.Comments.ContainsKey(new CellAddress(1, 3)));
        history.Undo();
        Assert.Equal(3, sheet.GetValue(new CellAddress(0, 1)).AsNumber());
        Assert.True(sheet.Comments.ContainsKey(new CellAddress(1, 1)));
    }

    [Fact]
    public void A_left_to_right_sort_shifts_relative_formulas_by_columns()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(2));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Number(1));
        sheet[new CellAddress(1, 0)] = new Cell { Formula = "=A1*10" };
        sheet[new CellAddress(1, 1)] = new Cell { Formula = "=B1*10" };

        new SheetSorter().Sort(sheet, new CellRange(CellAddress.First, new CellAddress(1, 1)), [new SortKey(0)], orientation: SortOrientation.LeftToRight);

        Assert.Equal(1, sheet.GetValue(CellAddress.First).AsNumber());
        Assert.Equal("=A1*10", sheet[new CellAddress(1, 0)].Formula);
        Assert.Equal("=B1*10", sheet[new CellAddress(1, 1)].Formula);
    }

    [Fact]
    public void A_sort_of_a_plain_range_is_remembered_on_the_sheet_and_undone()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var range = new CellRange(new CellAddress(2, 1), new CellAddress(9, 3));
        var memory = new SortSettingsMemory();
        var history = new UndoStack();

        memory.Remember(workbook, 0, range, [new SortKey(1, false)], history, "Sort", hasHeader: true);

        Assert.Equal(range, sheet.RangeSort!.Range);
        Assert.True(sheet.RangeSort.HasHeader);
        Assert.Equal([new SortField(1, false)], sheet.RangeSort.Settings.Fields);
        Assert.Equal([new SortField(1, false)], memory.Recall(workbook, 0, range)!.Fields);
        Assert.Null(memory.Recall(workbook, 0, new CellRange(new CellAddress(20, 1), new CellAddress(29, 3))));
        history.Undo();
        Assert.Null(sheet.RangeSort);
        history.Redo();
        Assert.NotNull(sheet.RangeSort);
    }

    [Fact]
    public void A_remembered_left_to_right_sort_keeps_its_orientation()
    {
        var workbook = Workbook.CreateDefault();
        var range = new CellRange(CellAddress.First, new CellAddress(2, 5));

        new SortSettingsMemory().Remember(workbook, 0, range, [new SortKey(2)], null, "Sort", orientation: SortOrientation.LeftToRight);

        var recalled = new SortSettingsMemory().Recall(workbook, 0, range)!;
        Assert.True(recalled.LeftToRight);
        Assert.Equal(2, recalled.Fields[0].ColumnOffset);
    }

    [Fact]
    public void The_plain_range_sort_follows_structure_edits()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(9, 9), CellValue.Number(1));
        sheet.RangeSort = new SheetRangeSort(new CellRange(new CellAddress(2, 2), new CellAddress(6, 5)), true, new SortSettings { Fields = [new SortField(2)] });
        var structure = new SheetStructure();

        structure.InsertColumns(engine, 0, 3, 1);
        Assert.Equal(new CellRange(new CellAddress(2, 2), new CellAddress(6, 6)), sheet.RangeSort!.Range);
        Assert.Equal(3, sheet.RangeSort.Settings.Fields[0].ColumnOffset);

        structure.InsertRows(engine, 0, 0, 2);
        Assert.Equal(new CellRange(new CellAddress(4, 2), new CellAddress(8, 6)), sheet.RangeSort!.Range);
        Assert.Equal(3, sheet.RangeSort.Settings.Fields[0].ColumnOffset);

        sheet.RangeSort = sheet.RangeSort with { Settings = new SortSettings { Fields = [new SortField(1)], Orientation = SortOrientation.LeftToRight } };
        structure.InsertRows(engine, 0, 5, 1);
        Assert.Equal(2, sheet.RangeSort!.Settings.Fields[0].ColumnOffset);

        structure.DeleteColumns(engine, 0, 2, 5);
        Assert.Null(sheet.RangeSort);
    }

    [Fact]
    public void A_copied_sheet_keeps_its_plain_range_sort()
    {
        var workbook = Workbook.CreateDefault();
        var remembered = new SheetRangeSort(new CellRange(CellAddress.First, new CellAddress(3, 1)), false, new SortSettings { Fields = [new SortField(1)] });
        workbook.Sheets[0].RangeSort = remembered;

        var copy = new WorkbookCopier().Copy(workbook);

        Assert.Equal(remembered, copy.Sheets[0].RangeSort);
    }

    [Fact]
    public void Sort_lists_match_values_in_the_spreadsheet_locale_not_the_thread_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var lists = new SortLists([["ink", "pen"]], new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("en-US")));

            Assert.NotNull(lists.ContainingAll(["INK", "PEN"]));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
