using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed class CellRowIndex
{
    private readonly Dictionary<int, List<int>> _rows = [];
    private bool _sealed;

    public CellRowIndex(IEnumerable<CellAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        foreach (var address in addresses) Add(address);
    }

    public int LastRow { get; private set; } = -1;

    public int ColumnCount
    {
        get
        {
            Seal();
            return _columnCount;
        }
    }

    private int _columnCount;

    public IEnumerable<int> Rows
    {
        get
        {
            Seal();
            return _rows.Keys.Order();
        }
    }

    public bool Contains(int row) => _rows.ContainsKey(row);

    public IReadOnlyList<int>? Columns(int row)
    {
        Seal();
        return _rows.GetValueOrDefault(row);
    }

    public void Add(CellAddress address)
    {
        if (_sealed) throw new InvalidOperationException("The index is already in use.");
        if (!_rows.TryGetValue(address.Row, out var columns)) _rows[address.Row] = columns = [];
        columns.Add(address.Column);
        LastRow = Math.Max(LastRow, address.Row);
    }

    private void Seal()
    {
        if (_sealed) return;
        _sealed = true;
        foreach (var columns in _rows.Values)
        {
            columns.Sort();
            var write = 0;
            for (var read = 0; read < columns.Count; read++)
                if (write == 0 || columns[read] != columns[write - 1]) columns[write++] = columns[read];
            columns.RemoveRange(write, columns.Count - write);
            _columnCount = Math.Max(_columnCount, columns[^1] + 1);
        }
    }
}
