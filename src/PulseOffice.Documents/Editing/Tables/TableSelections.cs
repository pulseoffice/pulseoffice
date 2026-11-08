using PulseOffice.Document.Tables;

namespace PulseOffice.Documents.Editing.Tables;

public interface ITableSelections
{
    TableSelection? Select(TextDocument document, DocumentPosition anchor, DocumentPosition caret);
}

public sealed class TableSelections(ITableEditing tables, ITableGrid grid, ITableSpans spans) : ITableSelections
{
    public TableSelection? Select(TextDocument document, DocumentPosition anchor, DocumentPosition caret)
    {
        if (tables.Locate(document, caret.ParagraphId) is not { } at) return null;
        var from = tables.Locate(document, anchor.ParagraphId) is { } start && ReferenceEquals(start.Table, at.Table) ? start : at;
        var range = new TableCellRange(from.Row, from.Column, at.Row, at.Column);
        return new TableSelection(at, spans.Enclose(range, grid.Spans(at.Table)));
    }
}
