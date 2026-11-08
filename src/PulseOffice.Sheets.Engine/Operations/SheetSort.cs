using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SortKey(int ColumnOffset, bool Ascending = true)
{
    public IReadOnlyList<string>? CustomOrder { get; init; }

    public bool CaseSensitive { get; init; }
}

public interface ISortLists
{
    IReadOnlyList<IReadOnlyList<string>> All { get; }

    IReadOnlyList<string>? ContainingAll(IEnumerable<string> values);
}

public sealed class SortLists(IReadOnlyList<IReadOnlyList<string>>? lists = null, ISpreadsheetLocale? locale = null) : ISortLists
{
    public IReadOnlyList<IReadOnlyList<string>> All { get; } = lists ?? [];

    private readonly ISpreadsheetLocale? _locale = locale;

    public IReadOnlyList<string>? ContainingAll(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var wanted = values.Where(value => value.Length > 0).ToList();
        if (wanted.Count == 0) return null;
        var compare = (_locale?.Culture ?? CultureInfo.CurrentCulture).CompareInfo;

        return All.FirstOrDefault(list =>
            wanted.All(value => list.Any(item => compare.Compare(item, value, CompareOptions.IgnoreCase) == 0)));
    }
}

public interface ISheetSort
{
    void Sort(
        Worksheet sheet, CellRange range, IReadOnlyList<SortKey> keys,
        bool hasHeader = false, UndoStack? history = null, string historyLabel = "Sort",
        SortOrientation orientation = SortOrientation.TopToBottom);

    bool Splits(Worksheet sheet, CellRange range, bool hasHeader);
}

public sealed class SheetSorter : ISheetSort
{
    private readonly FormulaTranslator _translator = new();
    private readonly ISpreadsheetLocale _locale;

    public SheetSorter() : this(new SystemSpreadsheetLocale())
    {
    }

    public SheetSorter(ISpreadsheetLocale locale)
    {
        _locale = locale ?? throw new ArgumentNullException(nameof(locale));
    }

    public void Sort(
        Worksheet sheet, CellRange range, IReadOnlyList<SortKey> keys,
        bool hasHeader = false, UndoStack? history = null, string historyLabel = "Sort",
        SortOrientation orientation = SortOrientation.TopToBottom)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(keys);
        var axis = new SortAxis(orientation == SortOrientation.LeftToRight);
        if (keys.Count == 0 || axis.Lines(range) < 2 || Splits(sheet, range, hasHeader, axis)) return;
        if (Occupied(sheet, range) is not { } occupied) return;
        range = occupied;

        var firstLine = axis.FirstLine(range) + (hasHeader ? 1 : 0);
        if (firstLine > axis.LastLine(range)) return;

        var lines = new List<(int OriginalRow, Cell[] Cells)>();
        var width = axis.Width(range);

        for (var line = firstLine; line <= axis.LastLine(range); line++)
        {
            var cells = new Cell[width];

            for (var offset = 0; offset < width; offset++)
                cells[offset] = sheet[axis.At(range, line, offset)];

            lines.Add((line, cells));
        }

        var sorted = lines.OrderBy(r => r, new RowComparer(keys, _locale.Culture.CompareInfo)).ToList();

        if (sorted.Select(r => r.OriginalRow).SequenceEqual(lines.Select(r => r.OriginalRow))) return;

        using var transaction = history?.BeginTransaction(historyLabel);

        MoveComments(sheet, range, firstLine, sorted.Select(r => r.OriginalRow).ToList(), historyLabel, history, axis);

        var before = lines.Select(line => line.Cells).ToList();
        var after = new List<Cell[]>(sorted.Count);
        for (var index = 0; index < sorted.Count; index++)
        {
            var (originalLine, cells) = sorted[index];
            var (rowDelta, columnDelta) = axis.Delta(firstLine + index - originalLine);
            after.Add([.. cells.Select(cell => Translate(cell, rowDelta, columnDelta))]);
        }

        var block = new SortedBlock(sheet, range, firstLine, axis);
        block.Write(after);
        history?.Push(new DelegateAction(historyLabel, redo: () => block.Write(after), undo: () => block.Write(before)));
    }

    private sealed class SortAxis(bool acrossColumns)
    {
        public int Lines(CellRange range) => acrossColumns ? range.ColumnCount : range.RowCount;

        public int FirstLine(CellRange range) => acrossColumns ? range.Start.Column : range.Start.Row;

        public int LastLine(CellRange range) => acrossColumns ? range.End.Column : range.End.Row;

        public int Width(CellRange range) => acrossColumns ? range.RowCount : range.ColumnCount;

        public CellAddress At(CellRange range, int line, int offset) =>
            acrossColumns ? new CellAddress(range.Start.Row + offset, line) : new CellAddress(line, range.Start.Column + offset);

        public int LineOf(CellAddress address) => acrossColumns ? address.Column : address.Row;

        public bool Across(CellRange range, CellAddress address) => acrossColumns
            ? address.Row >= range.Start.Row && address.Row <= range.End.Row
            : address.Column >= range.Start.Column && address.Column <= range.End.Column;

        public CellAddress WithLine(CellAddress address, int line) =>
            acrossColumns ? new CellAddress(address.Row, line) : new CellAddress(line, address.Column);

        public (int Rows, int Columns) Delta(int lines) => acrossColumns ? (0, lines) : (lines, 0);
    }

    private sealed class SortedBlock(Worksheet sheet, CellRange range, int firstLine, SortAxis axis)
    {
        public void Write(IReadOnlyList<Cell[]> lines)
        {
            for (var index = 0; index < lines.Count; index++)
                for (var offset = 0; offset < lines[index].Length; offset++)
                    sheet[axis.At(range, firstLine + index, offset)] = lines[index][offset];
        }
    }

    public bool Splits(Worksheet sheet, CellRange range, bool hasHeader) => Splits(sheet, range, hasHeader, new SortAxis(false));

    private bool Splits(Worksheet sheet, CellRange range, bool hasHeader, SortAxis axis)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (Occupied(sheet, range) is not { } occupied) return false;
        for (var line = axis.FirstLine(occupied) + (hasHeader ? 1 : 0); line <= axis.LastLine(occupied); line++)
            for (var offset = 0; offset < axis.Width(occupied); offset++)
            {
                var cell = sheet[axis.At(occupied, line, offset)];
                if (cell.IsPartOfArrayFormula || cell.IsSpilled || cell.SpillRange is not null) return true;
            }
        return false;
    }

    private CellRange? Occupied(Worksheet sheet, CellRange range)
    {
        var bottom = Math.Max(sheet.LastUsedRow, sheet.Comments.Keys.Select(address => address.Row).DefaultIfEmpty(-1).Max());
        var right = Math.Max(sheet.LastUsedColumn, sheet.Comments.Keys.Select(address => address.Column).DefaultIfEmpty(-1).Max());
        if (bottom < range.Start.Row || right < range.Start.Column) return null;

        return new CellRange(
            range.Start,
            new CellAddress(Math.Min(range.End.Row, bottom), Math.Min(range.End.Column, right)));
    }

    private void MoveComments(
        Worksheet sheet, CellRange range, int firstLine, IReadOnlyList<int> order, string historyLabel, UndoStack? history, SortAxis axis)
    {
        var inside = sheet.Comments
            .Where(pair => axis.LineOf(pair.Key) >= firstLine && axis.LineOf(pair.Key) <= axis.LastLine(range) && axis.Across(range, pair.Key))
            .ToList();
        if (inside.Count == 0) return;

        var target = new Dictionary<int, int>();
        for (var index = 0; index < order.Count; index++) target[order[index]] = firstLine + index;

        var moved = inside
            .Select(pair => (Address: axis.WithLine(pair.Key, target[axis.LineOf(pair.Key)]), Comment: pair.Value))
            .ToList();

        void Apply(IEnumerable<KeyValuePair<CellAddress, CellComment>> remove, IEnumerable<(CellAddress, CellComment)> add)
        {
            foreach (var pair in remove) sheet.Comments.Remove(pair.Key);
            foreach (var (address, comment) in add) sheet.Comments[address] = comment;
        }

        var before = inside.Select(pair => (pair.Key, pair.Value)).ToList();
        var after = moved.Select(pair => new KeyValuePair<CellAddress, CellComment>(pair.Address, pair.Comment)).ToList();

        Apply(inside, moved);

        history?.Push(new DelegateAction(
            historyLabel,
            redo: () => Apply(inside, moved),
            undo: () => Apply(after, before)));
    }

    private Cell Translate(Cell cell, int rowDelta, int columnDelta)
    {
        if (!cell.HasFormula || (rowDelta == 0 && columnDelta == 0)) return cell;

        var translated = _translator.Translate(cell.Formula!, rowDelta, columnDelta);
        return translated is null ? cell : cell with { Formula = translated };
    }

    private sealed class RowComparer(IReadOnlyList<SortKey> keys, CompareInfo compare)
    : IComparer<(int OriginalRow, Cell[] Cells)>
    {
        public int Compare((int OriginalRow, Cell[] Cells) x, (int OriginalRow, Cell[] Cells) y)
        {
            foreach (var key in keys)
            {
                if (key.ColumnOffset < 0 || key.ColumnOffset >= x.Cells.Length) continue;

                var first = x.Cells[key.ColumnOffset].Value;
                var second = y.Cells[key.ColumnOffset].Value;
                if (first.IsEmpty != second.IsEmpty) return first.IsEmpty ? 1 : -1;

                if (ListedFirst(first, second, key) is { } listed) return listed;

                var result = CompareValues(first, second, key);
                if (result == 0) continue;

                return key.Ascending ? result : -result;
            }

            return x.OriginalRow.CompareTo(y.OriginalRow);
        }

        private int? ListedFirst(CellValue a, CellValue b, SortKey key)
        {
            if (key.CustomOrder is not { Count: > 0 } order) return null;

            var first = IndexIn(order, a) >= 0;
            var second = IndexIn(order, b) >= 0;

            return first == second ? null : first ? -1 : 1;
        }

        private int CompareValues(CellValue a, CellValue b, SortKey key)
        {
            if (key.CustomOrder is { Count: > 0 } order)
            {
                var first = IndexIn(order, a);
                var second = IndexIn(order, b);

                if (first >= 0 && second >= 0) return first.CompareTo(second);
            }

            if (a.IsNumeric && b.IsNumeric) return a.AsNumber().CompareTo(b.AsNumber());

            if (a.Kind == ValueKind.Text && b.Kind == ValueKind.Text)
                return compare.Compare(a.AsText(), b.AsText(), key.CaseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase);

            return Rank(a).CompareTo(Rank(b));
        }

        private int IndexIn(IReadOnlyList<string> order, CellValue value)
        {
            var text = value.Kind == ValueKind.Text ? value.AsText() : value.ToString();

            for (var index = 0; index < order.Count; index++)
                if (compare.Compare(order[index], text, CompareOptions.IgnoreCase) == 0)
                    return index;

            return -1;
        }

        private int Rank(CellValue value) => value.Kind switch
        {
            ValueKind.Number or ValueKind.Boolean => 0,
            ValueKind.Text => 1,
            ValueKind.Empty => 3,
            _ => 2,
        };
    }
}
