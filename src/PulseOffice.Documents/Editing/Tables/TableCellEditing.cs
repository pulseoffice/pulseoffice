using PulseOffice.Document.Commands;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Editing.Tables;

public interface ITableCellEditing
{
    bool CanMerge(TableSelection selection);

    DocumentEdit Merge(TextDocument document, UndoStack history, TableSelection selection);

    bool IsMerged(TableSelection selection);

    DocumentEdit Unmerge(TextDocument document, UndoStack history, TableSelection selection);

    DocumentEdit Split(TextDocument document, UndoStack history, TableSelection selection, int parts, bool horizontally);
}

public sealed class TableCellEditing(ITableGrid grid, ITableCellFactory cells) : ITableCellEditing
{
    public bool CanMerge(TableSelection selection) => !selection.Range.IsSingleCell;

    public DocumentEdit Merge(TextDocument document, UndoStack history, TableSelection selection)
    {
        var table = selection.Table;
        var range = selection.Range;
        var recorder = new EditRecorder(document, "merge-table-cells").Touch(table);
        var members = grid.Cells(table, range).ToList();
        var origin = members[0];
        foreach (var member in members.Skip(1))
        {
            recorder.Touch(member.Cell.Content).Touch(origin.Cell.Content);
            var moved = member.Cell.Content.Blocks.Where(block => block is not ParagraphBlock { Length: 0 }).ToList();
            if (moved.Count > 0)
            {
                if (origin.Cell.Content.Blocks is [ParagraphBlock { Length: 0 }]) origin.Cell.Content.RemoveAt(0);
                foreach (var block in moved) origin.Cell.Content.Add(block);
            }
            member.Cell.Content.Replace([]);
            member.Cell.Covered = true;
            member.Cell.ColumnSpan = 1;
            member.Cell.RowSpan = 1;
        }
        for (var r = range.Top; r <= range.Bottom; r++)
            for (var c = range.Left; c <= range.Right; c++)
                if (r != range.Top || c != range.Left) table.Rows[r].Cells[c].Covered = true;
        origin.Cell.RowSpan = range.RowCount;
        origin.Cell.ColumnSpan = range.ColumnCount;
        return recorder.Commit(history);
    }

    public bool IsMerged(TableSelection selection) =>
        grid.Cells(selection.Table, selection.Range).Any(cell => cell.Cell.RowSpan > 1 || cell.Cell.ColumnSpan > 1);

    public DocumentEdit Unmerge(TextDocument document, UndoStack history, TableSelection selection)
    {
        var table = selection.Table;
        var recorder = new EditRecorder(document, "split-table-cells").Touch(table);
        foreach (var origin in grid.Cells(table, selection.Range).Where(cell => cell.Cell.RowSpan > 1 || cell.Cell.ColumnSpan > 1).ToList())
        {
            for (var r = origin.Row; r < origin.Row + origin.Cell.RowSpan && r < table.Rows.Count; r++)
            {
                for (var c = origin.Column; c < origin.Column + origin.Cell.ColumnSpan && c < table.Rows[r].Cells.Count; c++)
                {
                    if (r == origin.Row && c == origin.Column) continue;
                    var covered = table.Rows[r].Cells[c];
                    covered.Covered = false;
                    covered.RowSpan = 1;
                    covered.ColumnSpan = 1;
                    covered.Background = origin.Cell.Background;
                    covered.Borders = origin.Cell.Borders;
                    if (covered.Content.Count == 0)
                    {
                        recorder.Touch(covered.Content);
                        covered.Content.Replace(cells.Create(document).Content.Blocks);
                    }
                }
            }
            origin.Cell.RowSpan = 1;
            origin.Cell.ColumnSpan = 1;
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Split(TextDocument document, UndoStack history, TableSelection selection, int parts, bool horizontally)
    {
        if (IsMerged(selection)) return Unmerge(document, history, selection);
        var table = selection.Table;
        var target = selection.At;
        var recorder = new EditRecorder(document, "split-table-cells").Touch(table);
        parts = Math.Clamp(parts, 2, 32);
        if (horizontally) SplitRows(document, table, target.Row, target.Column, parts);
        else SplitColumns(document, table, target.Row, target.Column, parts);
        return recorder.Commit(history);
    }

    private void SplitRows(TextDocument document, TableBlock table, int row, int column, int parts)
    {
        var source = table.Rows[row];
        var raised = new HashSet<TableCell>(ReferenceEqualityComparer.Instance);
        for (var part = 1; part < parts; part++)
        {
            var added = new TableRow(source.Cells.Select((_, c) => c == column ? cells.Create(document) : new TableCell(new BlockContainer()) { Covered = true }))
            {
                Height = source.Height,
                MinimumHeight = source.MinimumHeight,
                AllowBreakAcrossPages = source.AllowBreakAcrossPages,
            };
            added.Cells[column].Background = source.Cells[column].Background;
            added.Cells[column].Borders = source.Cells[column].Borders;
            table.Rows.Insert(row + part, added);
        }
        for (var c = 0; c < source.Cells.Count; c++)
        {
            if (c == column) continue;
            if (grid.Origin(table, row, c) is { } origin && raised.Add(origin.Cell)) origin.Cell.RowSpan = Math.Max(1, origin.Cell.RowSpan) + parts - 1;
        }
        if (source.Height is { } height) foreach (var r in Enumerable.Range(row, parts)) table.Rows[r].Height = new Twip(height.Value / parts);
    }

    private void SplitColumns(TextDocument document, TableBlock table, int row, int column, int parts)
    {
        var widened = new HashSet<TableCell>(ReferenceEqualityComparer.Instance);
        var width = column < table.Columns.Count ? table.Columns[column].Value : 1440;
        var share = width / parts;
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var origin = grid.Origin(table, r, column);
            for (var part = 1; part < parts; part++)
            {
                var index = Math.Min(column + part, table.Rows[r].Cells.Count);
                if (r == row)
                {
                    var created = cells.Create(document);
                    created.Background = table.Rows[r].Cells[column].Background;
                    created.Borders = table.Rows[r].Cells[column].Borders;
                    table.Rows[r].Cells.Insert(index, created);
                }
                else
                {
                    table.Rows[r].Cells.Insert(index, new TableCell(new BlockContainer()) { Covered = true });
                }
            }
            if (r != row && origin is not null && widened.Add(origin.Cell)) origin.Cell.ColumnSpan = Math.Max(1, origin.Cell.ColumnSpan) + parts - 1;
        }
        if (column < table.Columns.Count)
        {
            table.Columns[column] = new Twip(width - share * (parts - 1));
            table.Columns.InsertRange(column + 1, Enumerable.Repeat(new Twip(share), parts - 1));
        }
    }
}

public interface ITableCellFactory
{
    TableCell Create(TextDocument document, string text = "", string? styleName = null);
}

public sealed class TableCellFactory : ITableCellFactory
{
    public TableCell Create(TextDocument document, string text = "", string? styleName = null) =>
        new(new BlockContainer([new ParagraphBlock(document.NextId("p"), text, styleName ?? DefaultStyleNames.TableContents)]));
}
