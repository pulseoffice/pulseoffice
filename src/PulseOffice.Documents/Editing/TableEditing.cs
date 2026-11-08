using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Editing;

public sealed record TableCellLocation(TableBlock Table, int Row, int Column, BlockContainer Owner);

public interface ITableEditing
{
    TableCellLocation? Locate(TextDocument document, string paragraphId);

    TableCellLocation? LocateRow(TextDocument document, TableBlock table, int row);

    DocumentEdit InsertRow(TextDocument document, UndoStack history, TableCellLocation at, bool below);

    DocumentEdit InsertColumn(TextDocument document, UndoStack history, TableCellLocation at, bool right);

    DocumentEdit DeleteRow(TextDocument document, UndoStack history, TableCellLocation at);

    DocumentEdit DeleteColumn(TextDocument document, UndoStack history, TableCellLocation at);

    DocumentEdit DeleteTable(TextDocument document, UndoStack history, TableCellLocation at);
}

public sealed class TableEditing(IStoryCatalog stories, IRangeDeletion deletion) : ITableEditing
{
    public TableCellLocation? Locate(TextDocument document, string paragraphId)
    {
        var cellContainer = stories.Find(document, paragraphId).Location.Container;
        foreach (var story in stories.Stories(document))
        {
            if (Search(story, cellContainer) is { } found) return found;
        }
        return null;
    }

    public TableCellLocation? LocateRow(TextDocument document, TableBlock table, int row)
    {
        if (row < 0 || row >= table.Rows.Count) return null;
        foreach (var story in stories.Stories(document))
        {
            if (Owner(story, table) is { } owner) return new TableCellLocation(table, row, 0, owner);
        }
        return null;
    }

    public DocumentEdit InsertRow(TextDocument document, UndoStack history, TableCellLocation at, bool below)
    {
        var table = at.Table;
        var recorder = new EditRecorder(document, "insert-table-row").Touch(table);
        var index = at.Row + (below ? 1 : 0);
        var width = table.Rows[at.Row].Cells.Count;
        var source = table.Rows[at.Row];
        var heading = at.Row < table.HeaderRows && index >= table.HeaderRows;
        var row = new TableRow(Enumerable.Range(0, width).Select(column => Like(document, column < source.Cells.Count && !heading ? source.Cells[column] : null)))
        {
            Height = source.Height,
            MinimumHeight = source.MinimumHeight,
            AllowBreakAcrossPages = source.AllowBreakAcrossPages,
        };
        var grown = new HashSet<TableCell>(ReferenceEqualityComparer.Instance);
        for (var column = 0; column < width && index < table.Rows.Count; column++)
        {
            if (Origin(table, index, column) is not { } origin || origin.Row >= index) continue;
            if (grown.Add(origin.Cell)) origin.Cell.RowSpan++;
            row.Cells[column].Covered = true;
        }
        table.Rows.Insert(index, row);
        if (index < table.HeaderRows) table.HeaderRows++;
        return recorder.Commit(history);
    }

    public DocumentEdit InsertColumn(TextDocument document, UndoStack history, TableCellLocation at, bool right)
    {
        var table = at.Table;
        var recorder = new EditRecorder(document, "insert-table-column").Touch(table);
        var index = at.Column + (right ? 1 : 0);
        var widened = new HashSet<TableCell>(ReferenceEqualityComparer.Instance);
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var cells = table.Rows[row].Cells;
            var cell = Like(document, at.Column < cells.Count ? cells[at.Column] : null);
            if (index < table.Rows[row].Cells.Count && Origin(table, row, index) is { } origin && origin.Column < index)
            {
                if (widened.Add(origin.Cell)) origin.Cell.ColumnSpan++;
                cell.Covered = true;
            }
            table.Rows[row].Cells.Insert(Math.Min(index, table.Rows[row].Cells.Count), cell);
        }
        var width = table.Columns.Count > 0 ? table.Columns[Math.Min(at.Column, table.Columns.Count - 1)] : new Twip(1440);
        table.Columns.Insert(Math.Min(index, table.Columns.Count), width);
        return recorder.Commit(history);
    }

    public DocumentEdit DeleteRow(TextDocument document, UndoStack history, TableCellLocation at)
    {
        var table = at.Table;
        if (table.Rows.Count <= 1) return DeleteTable(document, history, at);
        var recorder = new EditRecorder(document, "delete-table-row").Touch(table);
        var row = at.Row;
        var shrunk = new HashSet<TableCell>(ReferenceEqualityComparer.Instance);
        var cells = table.Rows[row].Cells;
        var discarded = new List<TableCell>();
        for (var column = 0; column < cells.Count; column++)
        {
            var cell = cells[column];
            if (cell.Covered)
            {
                if (Origin(table, row, column) is { } origin && origin.Row < row && shrunk.Add(origin.Cell)) origin.Cell.RowSpan--;
                discarded.Add(cell);
                continue;
            }
            if (cell.RowSpan > 1 && row + 1 < table.Rows.Count && column < table.Rows[row + 1].Cells.Count)
            {
                discarded.Add(table.Rows[row + 1].Cells[column]);
                table.Rows[row + 1].Cells[column] = Heir(cell, cell.ColumnSpan, cell.RowSpan - 1);
            }
            else
            {
                discarded.Add(cell);
            }
        }
        Discard(document, recorder, discarded);
        table.Rows.RemoveAt(row);
        if (row < table.HeaderRows) table.HeaderRows--;
        return recorder.Commit(history);
    }

    public DocumentEdit DeleteColumn(TextDocument document, UndoStack history, TableCellLocation at)
    {
        var table = at.Table;
        if (table.Rows.All(row => row.Cells.Count <= 1)) return DeleteTable(document, history, at);
        var recorder = new EditRecorder(document, "delete-table-column").Touch(table);
        var column = at.Column;
        var shrunk = new HashSet<TableCell>(ReferenceEqualityComparer.Instance);
        var discarded = new List<TableCell>();
        var origins = Enumerable.Range(0, table.Rows.Count).Select(row => column < table.Rows[row].Cells.Count ? Origin(table, row, column) : null).ToList();
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var cells = table.Rows[row].Cells;
            if (column >= cells.Count) continue;
            var cell = cells[column];
            if (cell.Covered)
            {
                if (origins[row] is { } origin && origin.Column < column && shrunk.Add(origin.Cell)) origin.Cell.ColumnSpan--;
                discarded.Add(cell);
            }
            else if (cell.ColumnSpan > 1 && column + 1 < cells.Count)
            {
                discarded.Add(cells[column + 1]);
                cells[column + 1] = Heir(cell, cell.ColumnSpan - 1, cell.RowSpan);
            }
            else
            {
                discarded.Add(cell);
            }
        }
        Discard(document, recorder, discarded);
        for (var row = 0; row < table.Rows.Count; row++)
        {
            if (column < table.Rows[row].Cells.Count) table.Rows[row].Cells.RemoveAt(column);
        }
        if (column < table.Columns.Count && table.Columns.Count > 1) table.Columns.RemoveAt(column);
        return recorder.Commit(history);
    }

    public DocumentEdit DeleteTable(TextDocument document, UndoStack history, TableCellLocation at)
    {
        var recorder = new EditRecorder(document, "delete-table").Touch(at.Owner);
        var index = at.Owner.Blocks.ToList().FindIndex(block => ReferenceEquals(block, at.Table));
        deletion.Discard(document, recorder, [at.Table]);
        at.Owner.RemoveAt(index);
        if (!at.Owner.Blocks.OfType<ParagraphBlock>().Any())
            at.Owner.Insert(index, new ParagraphBlock(document.NextId("p"), string.Empty));
        return recorder.Commit(history);
    }

    private void Discard(TextDocument document, EditRecorder recorder, IEnumerable<TableCell> cells) =>
        deletion.Discard(document, recorder, cells.SelectMany(cell => cell.Content.Blocks));

    private TableCellLocation? Search(BlockContainer container, BlockContainer target)
    {
        foreach (var block in container.Blocks)
        {
            if (block is TableBlock table)
            {
                for (var row = 0; row < table.Rows.Count; row++)
                {
                    for (var column = 0; column < table.Rows[row].Cells.Count; column++)
                    {
                        var content = table.Rows[row].Cells[column].Content;
                        if (ReferenceEquals(content, target)) return new TableCellLocation(table, row, column, container);
                        if (Search(content, target) is { } nested) return nested;
                    }
                }
            }
            else if (block is SectionBlock section && Search(section.Content, target) is { } inSection)
            {
                return inSection;
            }
        }
        return null;
    }

    private BlockContainer? Owner(BlockContainer container, TableBlock target)
    {
        foreach (var block in container.Blocks)
        {
            if (ReferenceEquals(block, target)) return container;
            var inner = block switch
            {
                TableBlock table => table.Rows.SelectMany(row => row.Cells).Select(cell => Owner(cell.Content, target)).FirstOrDefault(found => found is not null),
                SectionBlock section => Owner(section.Content, target),
                _ => null,
            };
            if (inner is not null) return inner;
        }
        return null;
    }

    private sealed record CellOrigin(TableCell Cell, int Row, int Column);

    private CellOrigin? Origin(TableBlock table, int row, int column)
    {
        for (var r = 0; r <= row && r < table.Rows.Count; r++)
        {
            var cells = table.Rows[r].Cells;
            for (var c = 0; c <= column && c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell.Covered && r + cell.RowSpan > row && c + cell.ColumnSpan > column) return new CellOrigin(cell, r, c);
            }
        }
        return null;
    }

    private TableCell Heir(TableCell origin, int columnSpan, int rowSpan) =>
        new(origin.Content) { ColumnSpan = columnSpan, RowSpan = rowSpan, StyleName = origin.StyleName, Background = origin.Background, VerticalAlign = origin.VerticalAlign, Borders = origin.Borders, Formula = origin.Formula, Protected = origin.Protected, NumberFormat = origin.NumberFormat, TextDirection = origin.TextDirection, Padding = origin.Padding };

    private TableCell Like(TextDocument document, TableCell? model)
    {
        var style = model?.Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault()?.StyleName ?? DefaultStyleNames.TableContents;
        return new TableCell(new BlockContainer([new ParagraphBlock(document.NextId("p"), string.Empty, style)]))
        {
            Background = model?.Background,
            Borders = model?.Borders,
            VerticalAlign = model?.VerticalAlign ?? CellVerticalAlign.Top,
            TextDirection = model?.TextDirection ?? CellTextDirection.Horizontal,
            Padding = model?.Padding,
        };
    }
}
