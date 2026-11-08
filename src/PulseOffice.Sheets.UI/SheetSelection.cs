using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI;

public sealed class SheetSelection
{
    private CellAddress _anchor = CellAddress.First;
    private CellAddress _corner = CellAddress.First;
    private CellAddress _active = CellAddress.First;
    private int? _rowEntryColumn;

    public CellAddress Active => _active;

    public CellAddress Corner => _corner;

    public CellRange Range => new(_anchor, _corner);

    public bool IsSingleCell => Range.IsSingleCell;

    public event EventHandler? Changed;

    public void MoveTo(CellAddress address)
    {
        _rowEntryColumn = null;
        Place(address);
    }

    private void Place(CellAddress address)
    {
        if (!address.IsValid) return;

        _anchor = address;
        _corner = address;
        _active = address;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ExtendTo(CellAddress address)
    {
        if (!address.IsValid) return;

        _corner = address;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Move(int rowDelta, int columnDelta, bool extend)
    {
        var from = extend ? _corner : _active;
        var target = new CellAddress(
            Math.Clamp(from.Row + rowDelta, 0, CellAddress.MaxRows - 1),
            Math.Clamp(from.Column + columnDelta, 0, CellAddress.MaxColumns - 1));

        if (extend) ExtendTo(target);
        else MoveTo(target);
    }

    public void SelectRow(int row)
    {
        _anchor = new CellAddress(row, 0);
        _corner = new CellAddress(row, CellAddress.MaxColumns - 1);
        _active = _anchor;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SelectColumn(int column)
    {
        _anchor = new CellAddress(0, column);
        _corner = new CellAddress(CellAddress.MaxRows - 1, column);
        _active = _anchor;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Advance(bool byRow, bool backwards)
    {
        if (IsSingleCell)
        {
            AdvanceSingle(byRow, backwards);
            return;
        }

        var range = Range;
        var rows = range.End.Row - range.Start.Row + 1;
        var columns = range.End.Column - range.Start.Column + 1;
        long count = (long)rows * columns;
        var row = _active.Row - range.Start.Row;
        var column = _active.Column - range.Start.Column;
        long index = byRow ? (long)column * rows + row : (long)row * columns + column;
        index = ((index + (backwards ? -1 : 1)) % count + count) % count;

        _active = byRow
            ? new CellAddress(range.Start.Row + (int)(index % rows), range.Start.Column + (int)(index / rows))
            : new CellAddress(range.Start.Row + (int)(index / columns), range.Start.Column + (int)(index % columns));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AdvanceTo(CellAddress target, bool byRow, bool backwards)
    {
        var entryColumn = _rowEntryColumn;
        if (byRow) entryColumn = null;
        else if (!backwards) entryColumn ??= _active.Column;
        Place(target);
        _rowEntryColumn = entryColumn;
    }

    public CellAddress? RowEntryReturn(bool byRow, bool backwards) =>
        byRow && !backwards && _rowEntryColumn is { } column
            ? new CellAddress(Math.Min(_active.Row + 1, CellAddress.MaxRows - 1), column)
            : null;

    private void AdvanceSingle(bool byRow, bool backwards)
    {
        var target = RowEntryReturn(byRow, backwards) ?? new CellAddress(
                Math.Clamp(_active.Row + (byRow ? (backwards ? -1 : 1) : 0), 0, CellAddress.MaxRows - 1),
                Math.Clamp(_active.Column + (byRow ? 0 : (backwards ? -1 : 1)), 0, CellAddress.MaxColumns - 1));
        AdvanceTo(target, byRow, backwards);
    }
}
