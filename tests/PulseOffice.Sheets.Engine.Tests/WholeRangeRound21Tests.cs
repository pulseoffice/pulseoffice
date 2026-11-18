using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class WholeRangeRound21Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private const int UsedExtent = 16;
    private readonly GrowthCheck _growth = new(TimeSpan.FromMilliseconds(25), 4);

    private static CellRange Columns(int first, int last) => Columns(first, last, CellAddress.MaxRows);

    private static CellRange Columns(int first, int last, int rows) =>
        new(new CellAddress(0, first), new CellAddress(rows - 1, last));

    private static CellRange Rows(int first, int last) =>
        new(new CellAddress(first, 0), new CellAddress(last, CellAddress.MaxColumns - 1));

    private void AsCheapAsTheUsedRows(Func<int, Action> arrange) => _growth.Flat(arrange, UsedExtent, CellAddress.MaxRows);

    private void AsCheapAsTheUsedColumns(Func<int, Action> arrange) => _growth.Flat(arrange, UsedExtent, CellAddress.MaxColumns);

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
        Worksheet sheet = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            sheet = Few().Sheet;
            return () => new SheetSorter().Sort(sheet, Columns(0, 0, rows), [new SortKey(0)], history: new UndoStack());
        });
        Assert.Equal("a;2", sheet.GetValue(new CellAddress(0, 0)).AsText());
    }

    [Fact]
    public void Planning_duplicate_removal_on_a_whole_column_is_quick()
    {
        DuplicateRemovalPlan plan = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            var sheet = Few().Sheet;
            return () => plan = new DuplicateRemover(new SelectionWorkArea()).Plan(sheet, new DuplicateRemovalRequest(Columns(0, 1, rows), HasHeader: false));
        });
        Assert.Equal([2], plan.RowsToRemove);
    }

    [Fact]
    public void Planning_text_to_columns_on_a_whole_column_is_quick()
    {
        TextToColumnsPlan plan = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            var sheet = Few().Sheet;
            return () => plan = new TextToColumns(new PulseOffice.Foundation.Text.DelimitedText()).Plan(sheet, new TextToColumnsRequest(Columns(0, 0, rows)));
        });
        Assert.Equal(6, plan.Values.Count);
    }

    [Fact]
    public void Detecting_an_outline_on_whole_rows_is_quick()
    {
        DetectedOutline outline = null!;
        AsCheapAsTheUsedColumns(columns =>
        {
            var sheet = Few().Sheet;
            sheet.SetFormula(new CellAddress(4, 1), "=SUM(B1:B4)");
            return () => outline = new SheetOutline().Detect(sheet, new CellRange(new CellAddress(0, 0), new CellAddress(6, columns - 1)));
        });
        Assert.Equal(0, Assert.Single(outline.Rows).First);
    }

    [Fact]
    public void Collapsing_a_group_of_every_row_is_quick_and_expands_back()
    {
        Worksheet sheet = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            sheet = Few().Sheet;
            var outline = new SheetOutline();
            outline.Group(sheet, StructureAxis.Rows, 0, rows - 1);
            var current = sheet;
            return () =>
            {
                outline.Collapse(current, StructureAxis.Rows, 0);
                outline.Expand(current, StructureAxis.Rows, 0);
            };
        });

        Assert.False(sheet.Layout.IsRowHidden(CellAddress.MaxRows - 1));
        Assert.False(sheet.Layout.IsRowHidden(0));
    }

    [Fact]
    public void Inserting_and_deleting_cells_over_a_whole_column_is_quick()
    {
        Worksheet sheet = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            var (_, engine, few) = Few();
            sheet = few;
            var shifts = new SheetCellShift(new SheetStructure());
            return () =>
            {
                shifts.Insert(engine, 0, Columns(0, 0, rows), CellShift.Right);
                shifts.Delete(engine, 0, Columns(0, 0, rows), CellShift.Right);
            };
        });

        Assert.Equal("b;1", sheet.GetValue(new CellAddress(0, 0)).AsText());
        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 1)).AsNumber());
    }

    [Fact]
    public void Inserting_and_deleting_whole_columns_and_rows_is_quick()
    {
        var (_, engine, sheet) = Few();
        var structure = new SheetStructure();
        var shifts = new SheetCellShift(structure);
        void ShiftUsedCells()
        {
            shifts.Insert(engine, 0, Columns(0, 0, UsedExtent), CellShift.Right);
            shifts.Delete(engine, 0, Columns(0, 0, UsedExtent), CellShift.Right);
        }
        void InsertAndDeleteWholeLines()
        {
            structure.InsertColumns(engine, 0, 0, 1);
            structure.DeleteColumns(engine, 0, 0, 1);
            structure.InsertRows(engine, 0, 0, 1);
            structure.DeleteRows(engine, 0, 0, 1);
        }
        ShiftUsedCells();
        InsertAndDeleteWholeLines();
        var reference = _growth.Time(ShiftUsedCells);
        var whole = _growth.Time(InsertAndDeleteWholeLines);

        _growth.Bounded(reference, whole, 2, $"shifting {UsedExtent} cells {reference}, whole columns and rows {whole}");
        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 1)).AsNumber());
    }
    [Fact]
    public void Hiding_whole_columns_and_rows_is_quick()
    {
        Worksheet sheet = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            sheet = Few().Sheet;
            var current = sheet;
            var columns = Math.Min(rows, CellAddress.MaxColumns);
            return () =>
            {
                current.Layout.HideRows(0, rows - 1);
                current.Layout.ShowRows(0, rows - 1);
                current.Layout.HideColumns(0, columns - 1);
                current.Layout.ShowColumns(0, columns - 1);
            };
        });

        Assert.False(sheet.Layout.IsRowHidden(10));
    }

    [Fact]
    public void Undo_snapshots_of_a_sheet_with_whole_column_formats_are_quick()
    {
        Worksheet sheet = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            var (_, engine, few) = Few();
            sheet = few;
            if (rows == CellAddress.MaxRows) few.Layout.SetColumnStyles(0, 3, 0);
            else few.Layout.SetStyleBlock(Columns(0, 3, rows), 0);
            var snapshots = new SheetSnapshots();
            return () => snapshots.Restore(engine, 0, snapshots.Capture(few));
        });
        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 1)).AsNumber());
    }

    [Fact]
    public void Replacing_inside_a_whole_column_is_quick()
    {
        Worksheet sheet = null!;
        var replaced = 0;
        AsCheapAsTheUsedRows(rows =>
        {
            var (workbook, _, few) = Few();
            sheet = few;
            return () => replaced = new WorkbookSearch().ReplaceAll(workbook, 0, "b", "c", scope: Columns(0, 0, rows), history: new UndoStack());
        });

        Assert.Equal(2, replaced);
        Assert.Equal("c;1", sheet.GetValue(new CellAddress(2, 0)).AsText());
    }

    [Fact]
    public void Copying_and_pasting_whole_columns_is_quick()
    {
        Worksheet sheet = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            sheet = Few().Sheet;
            var current = sheet;
            var clipboard = new SheetClipboard();
            return () =>
            {
                var copied = clipboard.Copy(current, Columns(0, 1, rows));
                clipboard.Paste(copied, current, new CellAddress(0, 4), CellAddress.First, new PasteRequest(), new UndoStack());
            };
        });

        Assert.Equal(4, sheet.GetValue(new CellAddress(3, 5)).AsNumber());
    }

    [Fact]
    public void Pasting_links_to_whole_columns_writes_only_the_used_rows()
    {
        Worksheet sheet = null!;
        AsCheapAsTheUsedRows(rows =>
        {
            sheet = Few().Sheet;
            var current = sheet;
            var clipboard = new SheetClipboard();
            var copied = clipboard.Copy(current, Columns(0, 1, rows));
            return () => clipboard.Paste(copied, current, new CellAddress(0, 4), CellAddress.First, new PasteRequest { Link = true }, new UndoStack());
        });

        Assert.Equal("=B4", sheet[new CellAddress(3, 5)].Formula);
        Assert.True(sheet.LastUsedRow < 10);
    }

    [Fact]
    public void Copying_whole_columns_as_text_holds_only_the_used_rows()
    {
        var text = "";
        AsCheapAsTheUsedRows(rows =>
        {
            var sheet = Few().Sheet;
            var clipboard = new SheetClipboard();
            return () => text = clipboard.ToDelimitedText(clipboard.Copy(sheet, Columns(0, 1, rows)));
        });

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
