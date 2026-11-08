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

public sealed class Round122SortTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void A_large_sort_is_one_compact_undo_step()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        const int rows = 400;
        for (var row = 0; row < rows; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(rows - row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Text("r" + row));
        }
        var history = new UndoStack();

        new SheetSorter().Sort(sheet, new CellRange(CellAddress.First, new CellAddress(rows - 1, 1)), [new SortKey(0)], history: history);

        Assert.IsNotType<CompositeAction>(history.StatePoint);
        Assert.Equal(1, sheet.GetValue(CellAddress.First).AsNumber());
        history.Undo();
        Assert.Equal(rows, sheet.GetValue(CellAddress.First).AsNumber());
        Assert.Equal("r0", sheet.GetValue(new CellAddress(0, 1)).AsText());
        history.Redo();
        Assert.Equal(1, sheet.GetValue(CellAddress.First).AsNumber());
        Assert.Equal("r" + (rows - 1), sheet.GetValue(new CellAddress(0, 1)).AsText());
    }

    [Fact]
    public void Text_is_compared_in_the_spreadsheet_locale_not_the_thread_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var sheet = Workbook.CreateDefault().Sheets[0];
            sheet.SetValue(CellAddress.First, CellValue.Text("ä"));
            sheet.SetValue(new CellAddress(1, 0), CellValue.Text("z"));

            new SheetSorter(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("sv-SE")))
                .Sort(sheet, new CellRange(CellAddress.First, new CellAddress(1, 0)), [new SortKey(0)]);

            Assert.Equal("z", sheet.GetValue(CellAddress.First).AsText());
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Sort_settings_are_remembered_on_the_autofilter_and_the_database_range_and_undone()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var filtered = new CellRange(CellAddress.First, new CellAddress(9, 2));
        sheet.AutoFilter.Activate(filtered);
        var table = new CellRange(new CellAddress(0, 5), new CellAddress(9, 6));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Data", SheetIndex = 0, Range = table });
        var memory = new SortSettingsMemory();
        var history = new UndoStack();

        memory.Remember(workbook, 0, filtered, [new SortKey(2, false) { CaseSensitive = true }], history, "Sort");
        memory.Remember(workbook, 0, table, [new SortKey(1) { CustomOrder = ["b", "a"] }], history, "Sort");

        Assert.Equal([new SortField(2, false)], sheet.AutoFilter.Sort!.Fields);
        Assert.True(sheet.AutoFilter.Sort.CaseSensitive);
        Assert.Equal(["b", "a"], workbook.DatabaseRanges.Find("Data")!.Sort!.Fields[0].CustomOrder!);

        history.Undo();
        history.Undo();
        Assert.Null(sheet.AutoFilter.Sort);
        Assert.Null(workbook.DatabaseRanges.Find("Data")!.Sort);
        history.Redo();
        Assert.NotNull(sheet.AutoFilter.Sort);
        Assert.Equal([new SortField(1, false)], memory.Recall(workbook, 0, new CellRange(new CellAddress(0, 1), new CellAddress(9, 2)))!.Fields);
    }

    [Fact]
    public void Remembered_sort_fields_follow_inserted_columns()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(5, 3), CellValue.Number(1));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(5, 3)));
        sheet.AutoFilter.Remember(new SortSettings { Fields = [new SortField(2)] });

        new SheetStructure().InsertColumns(engine, 0, 1, 1);

        Assert.Equal(3, sheet.AutoFilter.Sort!.Fields[0].ColumnOffset);
    }
}
