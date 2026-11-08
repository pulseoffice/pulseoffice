using System.Diagnostics;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class WholeRangeRound21Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly TimeSpan Cap = TimeSpan.FromMilliseconds(400);

    private static CellRange Columns(int first, int last) =>
        new(new CellAddress(0, first), new CellAddress(CellAddress.MaxRows - 1, last));

    private static CellRange Rows(int first, int last) =>
        new(new CellAddress(first, 0), new CellAddress(last, CellAddress.MaxColumns - 1));

    private static void Within(Action action)
    {
        action();
        var clock = Stopwatch.StartNew();
        action();
        clock.Stop();
        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
    }

    private (Workbook Workbook, Recalculation.CalculationEngine Engine, Worksheet Sheet) Few()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("b;1"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("a;2"));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("b;1"));
        sheet.SetValue(new CellAddress(3, 1), CellValue.Number(4));
        return (workbook, engine, sheet);
    }

    [Fact]
    public void Sorting_a_whole_column_is_quick()
    {
        var (_, _, sheet) = Few();
        Within(() => new SheetSorter().Sort(sheet, Columns(0, 0), [new SortKey(0)], history: new UndoStack()));
        Assert.Equal("a;2", sheet.GetValue(new CellAddress(0, 0)).AsText());
    }

    [Fact]
    public void Planning_duplicate_removal_on_a_whole_column_is_quick()
    {
        var (_, _, sheet) = Few();
        DuplicateRemovalPlan plan = null!;
        Within(() => plan = new DuplicateRemover(new SelectionWorkArea()).Plan(sheet, new DuplicateRemovalRequest(Columns(0, 1), HasHeader: false)));
        Assert.Equal([2], plan.RowsToRemove);
    }

    [Fact]
    public void Planning_text_to_columns_on_a_whole_column_is_quick()
    {
        var (_, _, sheet) = Few();
        TextToColumnsPlan plan = null!;
        Within(() => plan = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Plan(sheet, new TextToColumnsRequest(Columns(0, 0))));
        Assert.Equal(6, plan.Values.Count);
    }

    [Fact]
    public void Detecting_an_outline_on_whole_rows_is_quick()
    {
        var (_, _, sheet) = Few();
        sheet.SetFormula(new CellAddress(4, 1), "=SUM(B1:B4)");
        DetectedOutline outline = null!;
        Within(() => outline = new SheetOutline().Detect(sheet, Rows(0, 6)));
        Assert.Equal(0, Assert.Single(outline.Rows).First);
    }

    [Fact]
    public void Collapsing_a_group_of_every_row_is_quick_and_expands_back()
    {
        var (_, _, sheet) = Few();
        var outline = new SheetOutline();
        outline.Group(sheet, StructureAxis.Rows, 0, CellAddress.MaxRows - 1);

        var clock = Stopwatch.StartNew();
        outline.Collapse(sheet, StructureAxis.Rows, 0);
        outline.Expand(sheet, StructureAxis.Rows, 0);
        clock.Stop();

        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
        Assert.False(sheet.Layout.IsRowHidden(CellAddress.MaxRows - 1));
        Assert.False(sheet.Layout.IsRowHidden(0));
    }

    [Fact]
    public void Inserting_and_deleting_cells_over_a_whole_column_is_quick()
    {
        var (_, engine, sheet) = Few();
        var shifts = new SheetCellShift(new SheetStructure());
        var clock = Stopwatch.StartNew();
        shifts.Insert(engine, 0, Columns(0, 0), CellShift.Right);
        shifts.Delete(engine, 0, Columns(0, 0), CellShift.Right);
        clock.Stop();

        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
        Assert.Equal("b;1", sheet.GetValue(new CellAddress(0, 0)).AsText());
        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 1)).AsNumber());
    }

    [Fact]
    public void Inserting_and_deleting_whole_columns_and_rows_is_quick()
    {
        var (_, engine, sheet) = Few();
        var structure = new SheetStructure();
        var clock = Stopwatch.StartNew();
        structure.InsertColumns(engine, 0, 0, 1);
        structure.DeleteColumns(engine, 0, 0, 1);
        structure.InsertRows(engine, 0, 0, 1);
        structure.DeleteRows(engine, 0, 0, 1);
        clock.Stop();

        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 1)).AsNumber());
    }

    [Fact]
    public void Hiding_whole_columns_and_rows_is_quick()
    {
        var (_, _, sheet) = Few();
        var clock = Stopwatch.StartNew();
        sheet.Layout.HideRows(0, CellAddress.MaxRows - 1);
        sheet.Layout.ShowRows(0, CellAddress.MaxRows - 1);
        sheet.Layout.HideColumns(0, CellAddress.MaxColumns - 1);
        sheet.Layout.ShowColumns(0, CellAddress.MaxColumns - 1);
        clock.Stop();

        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
        Assert.False(sheet.Layout.IsRowHidden(10));
    }

    [Fact]
    public void Undo_snapshots_of_a_sheet_with_whole_column_formats_are_quick()
    {
        var (_, engine, sheet) = Few();
        sheet.Layout.SetColumnStyles(0, 3, 0);
        var snapshots = new SheetSnapshots();
        Within(() => snapshots.Restore(engine, 0, snapshots.Capture(sheet)));
        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 1)).AsNumber());
    }

    [Fact]
    public void Replacing_inside_a_whole_column_is_quick()
    {
        var (workbook, _, sheet) = Few();
        var replaced = 0;
        var clock = Stopwatch.StartNew();
        replaced = new WorkbookSearch().ReplaceAll(workbook, 0, "b", "c", scope: Columns(0, 0), history: new UndoStack());
        clock.Stop();

        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
        Assert.Equal(2, replaced);
        Assert.Equal("c;1", sheet.GetValue(new CellAddress(2, 0)).AsText());
    }

    [Fact]
    public void Copying_and_pasting_whole_columns_is_quick()
    {
        var (_, _, sheet) = Few();
        var clipboard = new SheetClipboard();
        var clock = Stopwatch.StartNew();
        var copied = clipboard.Copy(sheet, Columns(0, 1));
        clipboard.Paste(copied, sheet, new CellAddress(0, 4), CellAddress.First, new PasteRequest(), new UndoStack());
        clock.Stop();

        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 5)).AsNumber());
    }

    [Fact]
    public void Pasting_links_to_whole_columns_writes_only_the_used_rows()
    {
        var (_, _, sheet) = Few();
        var clipboard = new SheetClipboard();
        var copied = clipboard.Copy(sheet, Columns(0, 1));

        var clock = Stopwatch.StartNew();
        clipboard.Paste(copied, sheet, new CellAddress(0, 4), CellAddress.First, new PasteRequest { Link = true }, new UndoStack());
        clock.Stop();

        Assert.True(clock.Elapsed < Cap, clock.Elapsed.ToString());
        Assert.Equal("=B4", sheet[new CellAddress(3, 5)].Formula);
        Assert.True(sheet.LastUsedRow < 10);
    }

    [Fact]
    public void Copying_whole_columns_as_text_holds_only_the_used_rows()
    {
        var (_, _, sheet) = Few();
        var clipboard = new SheetClipboard();
        var text = "";

        Within(() => text = clipboard.ToDelimitedText(clipboard.Copy(sheet, Columns(0, 1))));

        Assert.Equal(4, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Copying_a_bounded_range_as_text_keeps_its_trailing_blank_rows()
    {
        var (_, _, sheet) = Few();
        var clipboard = new SheetClipboard();
        var range = new CellRange(CellAddress.First, new CellAddress(5, 0));

        Assert.Equal(6, clipboard.ToDelimitedText(clipboard.Copy(sheet, range)).Split('\n').Length - 1);
    }

    [Fact]
    public void Copying_a_whole_column_keeps_formatted_empty_cells_below_the_last_value()
    {
        var (workbook, _, sheet) = Few();
        var bold = workbook.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { BackgroundHex = "#FF0000" });
        sheet.Layout.SetStyleBlock(new CellRange(new CellAddress(6, 0), new CellAddress(8, 0)), bold);

        var copied = new SheetClipboard().Copy(sheet, Columns(0, 0));

        Assert.Contains(copied.Cells, entry => entry.Row == 8 && entry.Cell.StyleId == bold);
    }
}
