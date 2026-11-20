using Avalonia.Input;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;

namespace PulseOffice.Presentations.UI;

public abstract record TableKeyOutcome(TableCellRange Range);

public sealed record TableKeyMove(TableCellRange Range) : TableKeyOutcome(Range);

public sealed record TableKeyAppendRow(TableCellRange Range) : TableKeyOutcome(Range);

public sealed record TableKeyClear(TableCellRange Range) : TableKeyOutcome(Range);

public interface IDeckTableKeys
{
    TableKeyOutcome? Read(DataTableObject table, TableCellRange current, Key key, KeyModifiers modifiers);

    DataTableObject Apply(DataTableObject table, TableKeyOutcome outcome);

    DataTableObject Type(DataTableObject table, TableCellRange cell, string typed, bool replace);

    DataTableObject Erase(DataTableObject table, TableCellRange cell);

    string? Text(DataTableObject table, TableCellRange cell);

    DataTableObject Write(DataTableObject table, TableCellRange cell, string text);
}

public sealed class DeckTableKeys(ITableStructure structure) : IDeckTableKeys
{
    public TableKeyOutcome? Read(DataTableObject table, TableCellRange current, Key key, KeyModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(table);
        var columns = table.Snapshot.Columns.Count;
        if (columns == 0) return null;
        var top = table.Style.HeaderRow ? TableCellFormat.HeaderRow : 0;
        var bottom = table.Snapshot.Rows.Count - 1;
        if (bottom < top) return null;
        var index = new TableCellIndex(table);
        var (row, column) = Anchor(index, Math.Clamp(current.LastRow, top, bottom), Math.Clamp(current.LastColumn, 0, columns - 1));
        var span = index.Origin(row, column);
        var extend = modifiers == KeyModifiers.Shift;

        switch (key)
        {
            case Key.Tab when modifiers == KeyModifiers.None:
                return Walk(index, row, column, top, bottom, columns, 1) is { } next ? Single(next.Row, next.Column) : new TableKeyAppendRow(Cell(bottom + 1, 0));
            case Key.Tab when modifiers == KeyModifiers.Shift:
                return Walk(index, row, column, top, bottom, columns, -1) is { } previous ? Single(previous.Row, previous.Column) : Single(row, column);
            case Key.Delete or Key.Back when modifiers == KeyModifiers.None:
                return new TableKeyClear(current.Normalized);
            case Key.Left or Key.Right or Key.Up or Key.Down when extend:
                var (extendRow, extendColumn) = key switch
                {
                    Key.Left => (current.LastRow, Math.Max(0, current.LastColumn - 1)),
                    Key.Right => (current.LastRow, Math.Min(columns - 1, current.LastColumn + 1)),
                    Key.Up => (Math.Max(top, current.LastRow - 1), current.LastColumn),
                    _ => (Math.Min(bottom, current.LastRow + 1), current.LastColumn),
                };
                return new TableKeyMove(current with { LastRow = extendRow, LastColumn = extendColumn });
            case Key.Left or Key.Right or Key.Up or Key.Down when modifiers == KeyModifiers.None:
                var (nextRow, nextColumn) = key switch
                {
                    Key.Left => (row, Math.Max(0, column - 1)),
                    Key.Right => (row, Math.Min(columns - 1, column + (span?.ColumnSpan ?? 1))),
                    Key.Up => (Math.Max(top, row - 1), column),
                    _ => (Math.Min(bottom, row + (span?.RowSpan ?? 1)), column),
                };
                var target = Anchor(index, nextRow, nextColumn);
                return new TableKeyMove(Cell(target.Row, target.Column));
            default:
                return null;
        }
    }

    public DataTableObject Type(DataTableObject table, TableCellRange cell, string typed, bool replace)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(typed);
        var (row, column) = Anchor(new TableCellIndex(table), cell.FirstRow, cell.FirstColumn);
        var before = TextAt(table, row, column);
        return before is null ? table : WithText(table, row, column, replace ? typed : before + typed);
    }

    public string? Text(DataTableObject table, TableCellRange cell)
    {
        ArgumentNullException.ThrowIfNull(table);
        var (row, column) = Anchor(new TableCellIndex(table), cell.FirstRow, cell.FirstColumn);
        return TextAt(table, row, column);
    }

    public DataTableObject Write(DataTableObject table, TableCellRange cell, string text)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(text);
        var (row, column) = Anchor(new TableCellIndex(table), cell.FirstRow, cell.FirstColumn);
        var before = TextAt(table, row, column);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        return before is null || before == normalized ? table : WithText(table, row, column, normalized);
    }

    public DataTableObject Erase(DataTableObject table, TableCellRange cell)
    {
        ArgumentNullException.ThrowIfNull(table);
        var (row, column) = Anchor(new TableCellIndex(table), cell.FirstRow, cell.FirstColumn);
        var before = TextAt(table, row, column);
        if (string.IsNullOrEmpty(before)) return table;
        var cut = System.Globalization.StringInfo.ParseCombiningCharacters(before);
        return WithText(table, row, column, before[..cut[^1]]);
    }

    private static string? TextAt(DataTableObject table, int row, int column)
    {
        if (column < 0 || column >= table.Snapshot.Columns.Count) return null;
        if (row == TableCellFormat.HeaderRow) return table.Style.HeaderRow ? table.Snapshot.Columns[column].Name : null;
        if (row < 0 || row >= table.Snapshot.Rows.Count) return null;
        var values = table.Snapshot.Rows[row];
        return column < values.Count ? Convert.ToString(values[column], System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty : string.Empty;
    }

    private static DataTableObject WithText(DataTableObject table, int row, int column, string text)
    {
        table = table with
        {
            CellTexts = [.. table.CellTexts
                .Select(rich => rich.Row != row || rich.Column != column || rich.Matches(text) ? rich : rich.Rebased(text))
                .OfType<TableCellText>()],
        };
        var snapshot = table.Snapshot;
        if (row == TableCellFormat.HeaderRow)
            return table with { Snapshot = snapshot with { Columns = [.. snapshot.Columns.Select((c, i) => i == column ? c with { Name = text } : c)] } };
        var rows = snapshot.Rows.Select((values, index) =>
        {
            if (index != row) return values;
            object?[] copy = [.. values, .. Enumerable.Repeat<object?>(string.Empty, Math.Max(0, column + 1 - values.Count))];
            copy[column] = text;
            return (IReadOnlyList<object?>)copy;
        }).ToList();
        return table with { Snapshot = snapshot with { Rows = rows } };
    }

    private static (int Row, int Column) Anchor(TableCellIndex index, int row, int column) =>
        index.Cover(row, column) is { } merge ? (merge.Row, merge.Column) : (row, column);

    private static (int Row, int Column)? Walk(TableCellIndex index, int row, int column, int top, int bottom, int columns, int step)
    {
        var position = (row - top) * columns + column;
        var last = (bottom - top + 1) * columns;
        for (position += step; position >= 0 && position < last; position += step)
        {
            var (r, c) = (top + position / columns, position % columns);
            if (index.Cover(r, c) is null) return (r, c);
        }
        return null;
    }

    public DataTableObject Apply(DataTableObject table, TableKeyOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome switch
        {
            TableKeyAppendRow => structure.InsertRow(table, table.Snapshot.Rows.Count),
            TableKeyClear clear => Cleared(table, clear.Range),
            _ => table,
        };
    }

    private DataTableObject Cleared(DataTableObject table, TableCellRange range)
    {
        var snapshot = table.Snapshot;
        var changed = false;
        var rows = snapshot.Rows.Select((values, row) =>
        {
            if (row < range.Top || row > range.Bottom) return values;
            var copy = values.ToArray();
            for (var column = Math.Max(0, range.Left); column <= range.Right && column < copy.Length; column++)
            {
                if (copy[column] is null || copy[column] is string { Length: 0 }) continue;
                copy[column] = string.Empty;
                changed = true;
            }
            return (IReadOnlyList<object?>)copy;
        }).ToList();
        var headers = snapshot.Columns.Select((column, index) =>
        {
            if (!table.Style.HeaderRow || !range.Contains(TableCellFormat.HeaderRow, index) || column.Name.Length == 0) return column;
            changed = true;
            return column with { Name = string.Empty };
        }).ToList();
        return changed ? table with { Snapshot = snapshot with { Rows = rows, Columns = headers } } : table;
    }

    private TableKeyMove Single(int row, int column) => new(Cell(row, column));

    private TableCellRange Cell(int row, int column) => new(row, column, row, column);
}
