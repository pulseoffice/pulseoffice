using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;

namespace PulseOffice.Documents.Editing.Tables;

public interface ITablePasting
{
    bool HoldsTable(DocumentFragment? fragment);

    DocumentEdit PasteRows(TextDocument document, UndoStack history, TableCellLocation at, DocumentFragment fragment);

    DocumentEdit PasteColumns(TextDocument document, UndoStack history, TableCellLocation at, DocumentFragment fragment);
}

public sealed class TablePasting(IFragmentAdoption adoption, ITableSpanCutting spans) : ITablePasting
{
    public bool HoldsTable(DocumentFragment? fragment) =>
        fragment is not null && fragment.Blocks.Blocks.OfType<TableBlock>().Count() == 1
        && fragment.Blocks.Blocks.All(block => block is TableBlock or ParagraphBlock { Length: 0 });

    public DocumentEdit PasteRows(TextDocument document, UndoStack history, TableCellLocation at, DocumentFragment fragment)
    {
        var recorder = new EditRecorder(document, "paste-table-rows");
        if (!HoldsTable(fragment)) return recorder.Commit(history);
        var source = Source(document, recorder, fragment);
        var table = at.Table;
        recorder.Touch(table);
        var width = table.Rows[at.Row].Cells.Count;
        var rows = source.Rows.Select(row => new TableRow(Enumerable.Range(0, width).Select(column =>
            column < row.Cells.Count ? Plain(document, row.Cells[column]) : Empty(document)))
        {
            Height = row.Height,
            MinimumHeight = row.MinimumHeight,
            AllowBreakAcrossPages = row.AllowBreakAcrossPages,
        }).ToList();
        var heirs = spans.CutAbove(document, table, at.Row);
        table.Rows.InsertRange(at.Row, rows);
        foreach (var heir in heirs) table.Rows[at.Row + rows.Count].Cells[heir.Column] = heir.Cell;
        if (at.Row < table.HeaderRows) table.HeaderRows += rows.Count;
        return recorder.Commit(history);
    }

    public DocumentEdit PasteColumns(TextDocument document, UndoStack history, TableCellLocation at, DocumentFragment fragment)
    {
        var recorder = new EditRecorder(document, "paste-table-columns");
        if (!HoldsTable(fragment)) return recorder.Commit(history);
        var source = Source(document, recorder, fragment);
        var table = at.Table;
        recorder.Touch(table);
        var count = source.Columns.Count > 0 ? source.Columns.Count : source.Rows.Max(row => row.Cells.Count);
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var target = table.Rows[row].Cells;
            var column = Math.Min(at.Column, target.Count);
            var heir = spans.CutBefore(document, target, column);
            var cells = Enumerable.Range(0, count).Select(index =>
                row < source.Rows.Count && index < source.Rows[row].Cells.Count ? Plain(document, source.Rows[row].Cells[index]) : Empty(document));
            target.InsertRange(column, cells);
            if (heir is not null) target[column + count] = heir;
        }
        var widths = source.Columns.Count > 0 ? source.Columns : [.. Enumerable.Repeat(table.Columns.FirstOrDefault(), count)];
        table.Columns.InsertRange(Math.Min(at.Column, table.Columns.Count), widths);
        return recorder.Commit(history);
    }

    private TableBlock Source(TextDocument document, EditRecorder recorder, DocumentFragment fragment) =>
        adoption.Adopt(document, recorder, fragment).Blocks.Blocks.OfType<TableBlock>().Single();

    private TableCell Plain(TextDocument document, TableCell cell)
    {
        var copy = cell.Clone();
        copy.ColumnSpan = 1;
        copy.RowSpan = 1;
        copy.Covered = false;
        if (copy.Content.Count == 0) copy.Content.Add(new ParagraphBlock(document.NextId("p"), string.Empty));
        return copy;
    }

    private TableCell Empty(TextDocument document) => new(new BlockContainer([new ParagraphBlock(document.NextId("p"), string.Empty)]));
}
