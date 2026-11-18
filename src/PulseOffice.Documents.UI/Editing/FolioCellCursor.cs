using PulseOffice.Documents.Editing;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.UI.Editing;

public sealed class FolioCellCursor(FolioEditor editor)
{
    public void SelectCells(TableBlock table, TableCellRange range)
    {
        var first = table.Cell(range.Top, range.Left).Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault();
        var corner = table.Rows[range.Bottom].Cells[range.Right];
        if (corner.Covered)
            corner = Enumerable.Range(0, range.Right + 1).Reverse().Select(column => table.Rows[range.Bottom].Cells[column]).FirstOrDefault(cell => !cell.Covered) ?? corner;
        var last = corner.Content.Blocks.OfType<ParagraphBlock>().LastOrDefault();
        if (first is null || last is null) return;
        editor.Select(new DocumentPosition(first.Id, 0), new DocumentPosition(last.Id, last.Length));
    }

    public bool MoveToCell(bool backwards)
    {
        var tables = editor.Tables;
        if (tables.Editing.Locate(editor.Document, editor.Caret.ParagraphId) is not { } at) return false;
        tables.Recognition.Leave(editor, at);
        var table = at.Table;
        var row = at.Row;
        var column = at.Column;
        do
        {
            column += backwards ? -1 : 1;
            if (column >= table.Rows[row].Cells.Count)
            {
                column = 0;
                row++;
            }
            else if (column < 0)
            {
                row--;
                if (row < 0) return true;
                column = table.Rows[row].Cells.Count - 1;
            }
            if (row >= table.Rows.Count)
            {
                if (editor.RefusesEdit()) return true;
                tables.Editing.InsertRow(editor.Document, editor.History, at with { Row = table.Rows.Count - 1 }, true);
                editor.Normalize();
            }
        }
        while (table.Rows[row].Cells[column].Covered);
        var paragraphs = table.Rows[row].Cells[column].Content.Blocks.OfType<ParagraphBlock>().ToList();
        if (paragraphs.Count == 0) return true;
        editor.Select(new DocumentPosition(paragraphs[0].Id, 0), new DocumentPosition(paragraphs[^1].Id, paragraphs[^1].Length));
        return true;
    }

    public bool ResizeColumn(TableBlock table, int column, double points)
    {
        if (TableParagraphs(table).Any(editor.RefusesEdit)) return false;
        editor.Tables.Geometry.ResizeColumn(editor.Document, editor.History, table, column, points);
        editor.Normalize();
        editor.Refresh();
        return true;
    }

    private IEnumerable<string> TableParagraphs(TableBlock table) =>
        editor.ReadOnly() ? [editor.Caret.ParagraphId] : table.Rows.SelectMany(row => row.Cells).Select(cell => cell.Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault()?.Id).OfType<string>();
}
