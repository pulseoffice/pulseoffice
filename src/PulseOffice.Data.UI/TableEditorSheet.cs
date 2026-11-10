using PulseOffice.Document.Embedding;
using PulseOffice.Localization;

namespace PulseOffice.Data.UI;

public enum TableSheetChangeKind { Selection, Text, Structure }

public sealed class TableSheetChangedEventArgs(TableSheetChangeKind kind, IReadOnlyCollection<int> rows) : EventArgs
{
    public TableSheetChangeKind Kind { get; } = kind;

    public IReadOnlyCollection<int> Rows { get; } = rows;
}

public readonly record struct TableSelection(int FirstRow, int LastRow, int FirstColumn, int LastColumn)
{
    public bool Contains(TableCellRef cell) =>
        cell.Row >= FirstRow && cell.Row <= LastRow && cell.Column >= FirstColumn && cell.Column <= LastColumn;

    public bool IsSingle => FirstRow == LastRow && FirstColumn == LastColumn;

    public bool MixesHeaderAndBody => FirstRow == TableCellFormat.HeaderRow && LastRow >= 0;
}

public sealed class TableEditorSheet
{
    private readonly ITextCatalog _texts;
    private readonly ITableEditModel _model;
    private readonly TableEditHistory _history = new();
    private readonly List<string> _headers = [];
    private readonly List<List<string>> _rows = [];
    private readonly List<TableMerge> _merges = [];
    private readonly List<TableCellFormat> _cellFormats = [];
    private TableMergeIndex _index;

    public TableEditorSheet(DataTableObject table, ITextCatalog texts, ITableEditModel model)
    {
        ArgumentNullException.ThrowIfNull(table);
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _model = model ?? throw new ArgumentNullException(nameof(model));

        HeaderEmphasis = table.Style.HeaderRow;
        BandedRows = table.Style.BandedRows;

        var snapshot = table.Snapshot;
        foreach (var column in snapshot.Columns) _headers.Add(column.Name);
        foreach (var row in snapshot.Rows)
        {
            var line = new List<string>(_headers.Count);
            for (var column = 0; column < _headers.Count; column++)
                line.Add(column < row.Count ? _model.Display(row[column]) : string.Empty);
            _rows.Add(line);
        }
        _merges.AddRange(table.Merges);
        _cellFormats.AddRange(table.CellFormats);

        if (_headers.Count == 0) { _headers.Add(ColumnName(1)); foreach (var row in _rows) row.Add(string.Empty); }
        if (_rows.Count == 0) _rows.Add([.. _headers.Select(_ => string.Empty)]);

        _index = Reindex();
    }

    public event EventHandler<TableSheetChangedEventArgs>? Changed;

    public bool HeaderEmphasis { get; }

    public bool BandedRows { get; }

    public int RowCount => _rows.Count;

    public int ColumnCount => _headers.Count;

    public TableMergeIndex Index => _index;

    public IReadOnlyList<TableMerge> Merges => _merges;

    public TableCellRef Anchor { get; private set; } = new(0, 0);

    public TableCellRef Active { get; private set; } = new(0, 0);

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public TableSelection Selection => new(
        Math.Min(Anchor.Row, Active.Row), Math.Max(Anchor.Row, Active.Row),
        Math.Min(Anchor.Column, Active.Column), Math.Max(Anchor.Column, Active.Column));

    public bool CanMerge => Selection is { IsSingle: false, MixesHeaderAndBody: false };

    public bool CanSplit => _index.Owner(Active) is not null;

    public string Text(TableCellRef cell) =>
        cell.IsHeader ? _headers[cell.Column] : _rows[cell.Row][cell.Column];

    public string Text(int row, int column) => Text(new TableCellRef(row, column));

    public void Select(TableCellRef cell, bool extend = false)
    {
        var target = Settle(Clamp(cell));
        Active = target;
        if (!extend) Anchor = target;
        Announce(TableSheetChangeKind.Selection);
    }

    public void SelectRange(TableCellRef anchor, TableCellRef active)
    {
        Anchor = Clamp(anchor);
        Active = Clamp(active);
        Announce(TableSheetChangeKind.Selection);
    }

    public void SelectColumn(int column) =>
        SelectRange(new TableCellRef(_rows.Count - 1, column), new TableCellRef(0, column));

    public void SelectRow(int row) =>
        SelectRange(new TableCellRef(row, _headers.Count - 1), new TableCellRef(row, 0));

    public void SelectAll() =>
        SelectRange(new TableCellRef(_rows.Count - 1, _headers.Count - 1), new TableCellRef(0, 0));

    public void Move(int rowStep, int columnStep, bool extend = false)
    {
        var from = Active;
        var row = from.Row;
        var column = from.Column;
        if (_index.Owner(from) is { } owner)
        {
            row = rowStep > 0 ? owner.Row + owner.RowSpan - 1 : rowStep < 0 ? owner.Row : row;
            column = columnStep > 0 ? owner.Column + owner.ColumnSpan - 1 : columnStep < 0 ? owner.Column : column;
        }
        Select(new TableCellRef(row + rowStep, column + columnStep), extend);
    }

    public void Advance(bool backwards)
    {
        var owner = _index.Owner(Active);
        var column = backwards ? (owner?.Column ?? Active.Column) - 1 : (owner is null ? Active.Column : owner.Column + owner.ColumnSpan - 1) + 1;
        var row = Active.Row;
        if (column >= _headers.Count) { column = 0; row++; }
        if (column < 0) { column = _headers.Count - 1; row--; }
        if (row < TableCellFormat.HeaderRow || row >= _rows.Count) return;
        Select(new TableCellRef(row, column));
    }

    public void SetText(TableCellRef cell, string text)
    {
        text ??= string.Empty;
        var before = Text(cell);
        if (before == text) return;
        _history.Push(new CellTextStep([(cell, before, text)]));
        Write([(cell, text)]);
    }

    public void ClearSelection()
    {
        var selection = Selection;
        var changes = new List<(TableCellRef, string, string)>();
        for (var row = selection.FirstRow; row <= selection.LastRow; row++)
            for (var column = selection.FirstColumn; column <= selection.LastColumn; column++)
            {
                var cell = new TableCellRef(row, column);
                if (Text(cell).Length > 0) changes.Add((cell, Text(cell), string.Empty));
            }
        if (changes.Count == 0) return;
        _history.Push(new CellTextStep(changes));
        Write([.. changes.Select(change => (change.Item1, string.Empty))]);
    }

    public void Merge()
    {
        if (!CanMerge) return;
        var s = Selection;
        Restructure(() =>
        {
            _merges.RemoveAll(m => m.Row <= s.LastRow && m.Row + Math.Max(1, m.RowSpan) - 1 >= s.FirstRow
                && m.Column <= s.LastColumn && m.Column + m.ColumnSpan - 1 >= s.FirstColumn);
            _merges.Add(new TableMerge(s.FirstRow, s.FirstColumn, s.LastRow - s.FirstRow + 1, s.LastColumn - s.FirstColumn + 1));
            Anchor = Active = new TableCellRef(s.FirstRow, s.FirstColumn);
        });
    }

    public void Split()
    {
        if (_index.Owner(Active) is not { } owner) return;
        Restructure(() =>
        {
            _merges.RemoveAll(m => m.Row == owner.Row && m.Column == owner.Column);
            Anchor = Active = new TableCellRef(owner.Row, owner.Column);
        });
    }

    public void AppendRow() => Restructure(() =>
    {
        _rows.Add([.. _headers.Select(_ => string.Empty)]);
        Reformat(_model.WithAppendedRow(_cellFormats, _rows.Count - 1));
    });

    public void RemoveLastRow()
    {
        if (_rows.Count <= 1) return;
        Restructure(() =>
        {
            _rows.RemoveAt(_rows.Count - 1);
            TrimRows(_rows.Count);
        });
    }

    public void AppendColumn() => Restructure(() =>
    {
        _headers.Add(ColumnName(_headers.Count + 1));
        foreach (var row in _rows) row.Add(string.Empty);
        Reformat(_model.WithAppendedColumn(_cellFormats, _headers.Count - 1));
    });

    public void RemoveLastColumn()
    {
        if (_headers.Count <= 1) return;
        Restructure(() =>
        {
            _headers.RemoveAt(_headers.Count - 1);
            foreach (var row in _rows) row.RemoveAt(row.Count - 1);
            TrimColumns(_headers.Count);
        });
    }

    public void SetVerticalAlign(CellVerticalAlign align)
    {
        var s = Selection;
        Restructure(() => Reformat(_model.WithVerticalAlign(_cellFormats, s.FirstRow, s.LastRow, s.FirstColumn, s.LastColumn, align)));
    }

    public void Undo()
    {
        _history.Undo(this);
        Announce(TableSheetChangeKind.Selection);
    }

    public void Redo()
    {
        _history.Redo(this);
        Announce(TableSheetChangeKind.Selection);
    }

    public TableEdit Build(DataSnapshot original) => new(
        _model.Compose(original, _headers, _rows),
        [.. _merges],
        [.. _cellFormats]);

    internal void Write(IReadOnlyList<(TableCellRef Cell, string Text)> changes)
    {
        var rows = new HashSet<int>();
        foreach (var (cell, text) in changes)
        {
            if (cell.Column < 0 || cell.Column >= _headers.Count) continue;
            if (cell.IsHeader) _headers[cell.Column] = text;
            else if (cell.Row >= 0 && cell.Row < _rows.Count) _rows[cell.Row][cell.Column] = text;
            else continue;
            rows.Add(cell.Row);
        }
        Changed?.Invoke(this, new TableSheetChangedEventArgs(TableSheetChangeKind.Text, rows));
    }

    internal void Restore(TableSheetMemento memento)
    {
        _headers.Clear();
        _headers.AddRange(memento.Headers);
        _rows.Clear();
        foreach (var row in memento.Rows) _rows.Add([.. row]);
        _merges.Clear();
        _merges.AddRange(memento.Merges);
        _cellFormats.Clear();
        _cellFormats.AddRange(memento.CellFormats);
        Anchor = Clamp(Anchor);
        Active = Clamp(Active);
        _index = Reindex();
        Changed?.Invoke(this, new TableSheetChangedEventArgs(TableSheetChangeKind.Structure, []));
    }

    private TableSheetMemento Memento() =>
        new([.. _headers], [.. _rows.Select(row => row.ToArray())], [.. _merges], [.. _cellFormats]);

    private void Restructure(Action change)
    {
        var before = Memento();
        change();
        Anchor = Clamp(Anchor);
        Active = Clamp(Active);
        var after = Memento();
        _history.Push(new StructureStep(before, after));
        _index = Reindex();
        Changed?.Invoke(this, new TableSheetChangedEventArgs(TableSheetChangeKind.Structure, []));
    }

    private void Announce(TableSheetChangeKind kind) =>
        Changed?.Invoke(this, new TableSheetChangedEventArgs(kind, []));

    private TableMergeIndex Reindex() => new(_merges, _cellFormats, _rows.Count, _headers.Count);

    private TableCellRef Clamp(TableCellRef cell) => new(
        Math.Clamp(cell.Row, TableCellFormat.HeaderRow, _rows.Count - 1),
        Math.Clamp(cell.Column, 0, _headers.Count - 1));

    private TableCellRef Settle(TableCellRef cell) =>
        _index.Owner(cell) is { } owner ? new TableCellRef(owner.Row, owner.Column) : cell;

    private void Reformat(IReadOnlyList<TableCellFormat> formats)
    {
        var kept = formats.ToList();
        _cellFormats.Clear();
        _cellFormats.AddRange(kept);
    }

    private void TrimRows(int removed)
    {
        _cellFormats.RemoveAll(f => f.Row >= removed);
        for (var i = _merges.Count - 1; i >= 0; i--)
        {
            var merge = _merges[i];
            if (merge.Row == TableCellFormat.HeaderRow) continue;
            var span = Math.Min(merge.RowSpan, removed - merge.Row);
            if (span <= 0 || (span == 1 && merge.ColumnSpan == 1)) _merges.RemoveAt(i);
            else _merges[i] = merge with { RowSpan = span };
        }
    }

    private void TrimColumns(int removed)
    {
        _cellFormats.RemoveAll(f => f.Column >= removed);
        for (var i = _merges.Count - 1; i >= 0; i--)
        {
            var merge = _merges[i];
            var span = Math.Min(merge.ColumnSpan, removed - merge.Column);
            if (span <= 0 || (span == 1 && merge.RowSpan == 1)) _merges.RemoveAt(i);
            else _merges[i] = merge with { ColumnSpan = span };
        }
    }

    private string ColumnName(int index) => $"{_texts["deck.table.column"]} {index}";
}
