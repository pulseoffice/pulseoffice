namespace PulseOffice.UI.Controls.Tables.Insert;

public sealed record TableInsertLimits(int MaxRows, int MaxColumns)
{
    public static readonly TableInsertLimits Default = new(500, 64);
}

public sealed record TableGridExtent(int InitialRows, int InitialColumns, int MaxRows, int MaxColumns)
{
    public static readonly TableGridExtent Dialog = new(8, 10, 16, 20);

    public static readonly TableGridExtent Menu = new(8, 10, 14, 16);
}

public sealed class TableSizeModel
{
    private int _rows;
    private int _columns;
    private (int Rows, int Columns)? _hover;

    public TableSizeModel(TableInsertLimits limits, TableGridExtent extent, int rows, int columns)
    {
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        Extent = extent ?? throw new ArgumentNullException(nameof(extent));
        _rows = Math.Clamp(rows, 1, limits.MaxRows);
        _columns = Math.Clamp(columns, 1, limits.MaxColumns);
    }

    public event EventHandler? Changed;

    public TableInsertLimits Limits { get; }

    public TableGridExtent Extent { get; }

    public int Rows
    {
        get => _rows;
        set => Commit(value, _columns);
    }

    public int Columns
    {
        get => _columns;
        set => Commit(_rows, value);
    }

    public bool Hovering => _hover is not null;

    public int ShownRows => _hover?.Rows ?? _rows;

    public int ShownColumns => _hover?.Columns ?? _columns;

    public int GridRows => Fit(ShownRows, Extent.InitialRows, Extent.MaxRows, Limits.MaxRows);

    public int GridColumns => Fit(ShownColumns, Extent.InitialColumns, Extent.MaxColumns, Limits.MaxColumns);

    public void Hover(int rows, int columns)
    {
        var next = (Math.Clamp(rows, 1, Math.Min(Limits.MaxRows, GridRows)), Math.Clamp(columns, 1, Math.Min(Limits.MaxColumns, GridColumns)));
        if (_hover == next) return;
        _hover = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void EndHover()
    {
        if (_hover is null) return;
        _hover = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CommitHover()
    {
        if (_hover is not { } hover) return;
        _hover = null;
        _rows = hover.Rows;
        _columns = hover.Columns;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Commit(int rows, int columns)
    {
        var nextRows = Math.Clamp(rows, 1, Limits.MaxRows);
        var nextColumns = Math.Clamp(columns, 1, Limits.MaxColumns);
        if (nextRows == _rows && nextColumns == _columns && _hover is null) return;
        _hover = null;
        _rows = nextRows;
        _columns = nextColumns;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Move(int rows, int columns) => Commit(_rows + rows, _columns + columns);

    private int Fit(int shown, int initial, int visibleLimit, int limit) =>
        Math.Min(limit, Math.Max(initial, Math.Min(visibleLimit, shown + 1)));
}
