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
        var row = Math.Clamp(current.LastRow, top, bottom);
        var column = Math.Clamp(current.LastColumn, 0, columns - 1);
        var extend = modifiers == KeyModifiers.Shift;

        switch (key)
        {
            case Key.Tab when modifiers == KeyModifiers.None:
                if (column + 1 < columns) return Single(row, column + 1);
                return row < bottom ? Single(row + 1, 0) : new TableKeyAppendRow(Cell(bottom + 1, 0));
            case Key.Tab when modifiers == KeyModifiers.Shift:
                if (column > 0) return Single(row, column - 1);
                return row > top ? Single(row - 1, columns - 1) : Single(row, column);
            case Key.Delete or Key.Back when modifiers == KeyModifiers.None:
                return new TableKeyClear(current.Normalized);
            case Key.Left or Key.Right or Key.Up or Key.Down when modifiers is KeyModifiers.None or KeyModifiers.Shift:
                var (nextRow, nextColumn) = key switch
                {
                    Key.Left => (row, Math.Max(0, column - 1)),
                    Key.Right => (row, Math.Min(columns - 1, column + 1)),
                    Key.Up => (Math.Max(top, row - 1), column),
                    _ => (Math.Min(bottom, row + 1), column),
                };
                return new TableKeyMove(extend ? current with { LastRow = nextRow, LastColumn = nextColumn } : Cell(nextRow, nextColumn));
            default:
                return null;
        }
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
