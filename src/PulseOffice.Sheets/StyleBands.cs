using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed class StyleBands
{
    private readonly RunLengthMap<int> _rows = new();
    private readonly RunLengthMap<int> _columns = new();
    private readonly StyleBlocks _blocks = new();

    public RunLengthMap<int> Rows => _rows;

    public RunLengthMap<int> Columns => _columns;

    public StyleBlocks Blocks => _blocks;

    public int StyleAt(CellAddress address) =>
        !_blocks.IsEmpty && _blocks.TryGetValue(address.Row, address.Column, out var block) ? block
        : _rows.TryGetValue(address.Row, out var row) ? row
        : _columns.TryGetValue(address.Column, out var column) ? column
        : Styling.StyleCatalog.DefaultStyleId;

    public int LineStyleAt(CellAddress address) =>
        _rows.TryGetValue(address.Row, out var row) ? row
        : _columns.TryGetValue(address.Column, out var column) ? column
        : Styling.StyleCatalog.DefaultStyleId;

    public void SetRows(int first, int last, int? styleId) => Set(_rows, first, last, styleId);

    public void SetColumns(int first, int last, int? styleId) => Set(_columns, first, last, styleId);

    public void SetBlock(int firstRow, int lastRow, int firstColumn, int lastColumn, int styleId) =>
        _blocks.Set(firstRow, lastRow, firstColumn, lastColumn, styleId);

    public void InsertRows(int at, int count, int limit)
    {
        _rows.Insert(at, count, limit);
        _blocks.InsertRows(at, count, limit);
    }

    public void DeleteRows(int at, int count)
    {
        _rows.Delete(at, count);
        _blocks.DeleteRows(at, count);
    }

    public void InsertColumns(int at, int count, int limit)
    {
        _columns.Insert(at, count, limit);
        _blocks.InsertColumns(at, count, limit);
    }

    public void DeleteColumns(int at, int count)
    {
        _columns.Delete(at, count);
        _blocks.DeleteColumns(at, count);
    }

    public void CopyRow(int source, IReadOnlyCollection<int> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var present = _rows.TryGetValue(source, out var style);
        foreach (var target in targets) Set(_rows, target, target, present ? style : null);
        _blocks.CopyRow(source, targets);
    }

    public void CopyColumn(int source, IReadOnlyCollection<int> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var present = _columns.TryGetValue(source, out var style);
        foreach (var target in targets) Set(_columns, target, target, present ? style : null);
        _blocks.CopyColumn(source, targets);
    }

    public StyleBandsMemento Save()
    {
        var memento = new StyleBandsMemento();
        CopyTo(memento.Bands);
        return memento;
    }

    public void Restore(StyleBandsMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        memento.Bands.CopyTo(this);
    }

    public void CopyTo(StyleBands target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _rows.CopyTo(target._rows);
        _columns.CopyTo(target._columns);
        _blocks.CopyTo(target._blocks);
    }

    public bool SameAs(StyleBands other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _rows.Runs.SequenceEqual(other._rows.Runs) && _columns.Runs.SequenceEqual(other._columns.Runs) && _blocks.SameAs(other._blocks);
    }

    public void AddTo(ref HashCode hash)
    {
        foreach (var run in _columns.Runs) hash.Add(run);
        foreach (var run in _rows.Runs) hash.Add(run);
        _blocks.AddTo(ref hash);
    }

    private void Set(RunLengthMap<int> styles, int first, int last, int? styleId)
    {
        if (styleId is { } id && id != Styling.StyleCatalog.DefaultStyleId) styles.Set(first, last, id);
        else styles.Remove(first, last);
    }
}

public sealed class StyleBandsMemento
{
    internal StyleBands Bands { get; } = new();

    public bool SameAs(StyleBandsMemento other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Bands.SameAs(other.Bands);
    }
}
