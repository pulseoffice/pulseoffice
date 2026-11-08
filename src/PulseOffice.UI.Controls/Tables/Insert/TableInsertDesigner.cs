using PulseOffice.Document.Tables;

namespace PulseOffice.UI.Controls.Tables.Insert;

public sealed record TableInsertChoice(int Rows, int Columns, TableLook Look, bool HeaderRow, bool BandedRows);

public sealed record TableInsertRequest(string TitleKey)
{
    public int Rows { get; init; } = 3;

    public int Columns { get; init; } = 3;

    public TableInsertLimits Limits { get; init; } = TableInsertLimits.Default;

    public bool HeaderRow { get; init; } = true;

    public bool BandedRows { get; init; } = true;

    public string? LookKey { get; init; }

    public string ExplanationKey { get; init; } = "table.insert.explanation";

    public string ConfirmKey { get; init; } = "table.insert.confirm";
}

public sealed class TableInsertDesigner : ITableLookBanding
{
    public const int PreviewRowLimit = 12;
    public const int PreviewColumnLimit = 10;

    private int _look;
    private bool _headerRow;
    private bool _bandedRows;

    public TableInsertDesigner(IReadOnlyList<TableLook> looks, TableInsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(looks);
        ArgumentNullException.ThrowIfNull(request);
        if (looks.Count == 0) throw new ArgumentException("At least one table look is required.", nameof(looks));
        Looks = looks;
        Size = new TableSizeModel(request.Limits, TableGridExtent.Dialog, request.Rows, request.Columns);
        _look = Math.Max(0, looks.ToList().FindIndex(look => look.Key == request.LookKey));
        _headerRow = request.HeaderRow;
        _bandedRows = request.BandedRows;
        Size.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<TableLook> Looks { get; }

    public TableSizeModel Size { get; }

    public int LookIndex => _look;

    public TableLook Look => Looks[_look];

    public bool HeaderRow
    {
        get => _headerRow;
        set
        {
            if (_headerRow == value) return;
            _headerRow = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool BandedRows
    {
        get => _bandedRows;
        set
        {
            if (_bandedRows == value) return;
            _bandedRows = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public int PreviewRows => Math.Min(Size.ShownRows, PreviewRowLimit);

    public int PreviewColumns => Math.Min(Size.ShownColumns, PreviewColumnLimit);

    public bool PreviewTruncated => Size.ShownRows > PreviewRowLimit || Size.ShownColumns > PreviewColumnLimit;

    public void ChooseLook(int index)
    {
        if (index < 0 || index >= Looks.Count || index == _look) return;
        _look = index;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MoveLook(int delta) => ChooseLook(Math.Clamp(_look + delta, 0, Looks.Count - 1));

    public TableInsertChoice Choice() => new(Size.Rows, Size.Columns, Look, _headerRow, _bandedRows);
}
