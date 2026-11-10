using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public sealed record Cell
{
    private readonly CellDetails? _details;

    public CellValue Value { get; init; } = CellValue.Empty;

    public string? Formula { get; init; }

    public int StyleId { get; init; }

    public bool ExplicitStyle { get; init; }

    public string? StyleName { get => _details?.StyleName; init => _details = Detailed(Details with { StyleName = value }); }

    public string? Hyperlink { get => _details?.Hyperlink; init => _details = Detailed(Details with { Hyperlink = value }); }

    public CellRange? ArrayFormulaRange { get => _details?.ArrayFormulaRange; init => _details = Detailed(Details with { ArrayFormulaRange = value }); }

    public bool IsArrayAnchor { get => _details?.IsArrayAnchor == true; init => _details = Detailed(Details with { IsArrayAnchor = value }); }

    public CellRange? SpillRange { get => _details?.SpillRange; init => _details = Detailed(Details with { SpillRange = value }); }

    public bool IsSpilled { get => _details?.IsSpilled == true; init => _details = Detailed(Details with { IsSpilled = value }); }

    public bool ImplicitIntersection { get; init; }

    public bool UsesArrays { get; init; }

    public int InferredFormatId { get; init; }

    public bool HasFormula => Formula is not null;

    public bool IsPartOfArrayFormula => ArrayFormulaRange is not null;

    public bool HasStyle => ExplicitStyle || StyleId != Styling.StyleCatalog.DefaultStyleId;

    public static readonly Cell Blank = new();

    private CellDetails Details => _details ?? CellDetails.None;

    private static CellDetails? Detailed(CellDetails details) => details == CellDetails.None ? null : details;

    private sealed record CellDetails
    {
        public static readonly CellDetails None = new();

        public string? StyleName { get; init; }

        public string? Hyperlink { get; init; }

        public CellRange? ArrayFormulaRange { get; init; }

        public bool IsArrayAnchor { get; init; }

        public CellRange? SpillRange { get; init; }

        public bool IsSpilled { get; init; }
    }
}

public sealed class Worksheet
{
    private Dictionary<CellAddress, Cell> _cells = [];

    public Worksheet(string name)
    {
        Name = name;
        Merges.Changed += _ => Revision++;
        Layout.RowVisibilityChanged += _ => RowVisibilityChanged?.Invoke(this);
        _ = new SheetObjectFollower(this, new SheetObjectAnchoring());
    }

    public long Revision { get; private set; }

    public string Name { get; set; }

    public bool IsHidden { get; set; }

    public string? TabColorHex { get; set; }
    public int CellCount => _cells.Count;

    public SheetLayout Layout { get; } = new();

    public MergedRegions Merges { get; } = new();

    public AutoFilterState AutoFilter { get; } = new();

    public SheetRangeFilter? RangeFilter { get; set; }

    public SheetRangeSort? RangeSort { get; set; }

    public SparklineCollection Sparklines { get; } = new();

    public SortedSet<int> RowBreaks { get; } = [];

    public SortedSet<int> ColumnBreaks { get; } = [];

    public QueryBindingCollection QueryBindings { get; } = new();

    public List<PlacedObject> Objects { get; } = [];

    public List<Formatting.ConditionalRule> ConditionalRules { get; } = [];

    public List<Scenario> Scenarios { get; } = [];

    public List<DataValidation> Validations { get; } = [];

    public Dictionary<CellAddress, CellComment> Comments { get; } = [];

    public List<CellRange> PrintRanges { get; } = [];

    public LabelRangeCollection Labels { get; } = new();

    public SheetEventBindings Events { get; } = new();

    public bool RightToLeft { get; set; }

    public FrozenPanes Frozen { get; set; } = FrozenPanes.None;

    public List<TraceArrow> TraceArrows { get; } = [];

    public List<OutlineGroup> RowGroups { get; } = [];

    public List<OutlineGroup> ColumnGroups { get; } = [];

    public OutlineSummary OutlineSummary { get; set; } = OutlineSummary.Default;

    public string PageStyleName { get; set; } = "Default";

    public int RepeatedPrintRows { get; set; }

    public int RepeatedPrintColumns { get; set; }

    public SheetProtection Protection { get; set; } = SheetProtection.None;

    public Cell this[CellAddress address]
    {
        get => _cells.TryGetValue(address, out var c) ? c : Cell.Blank;
        set
        {
            var previous = _cells.TryGetValue(address, out var old) ? old : Cell.Blank;
            Store(address, value);
            if (previous == value) return;
            Revision++;
            CellTouched?.Invoke(this, address);
            if (previous.Value != value.Value || previous.Formula != value.Formula) ContentChanged?.Invoke(this, address);
        }
    }

    public event Action<Worksheet, CellAddress>? ContentChanged;

    public event Action<Worksheet, CellAddress>? CellTouched;

    public event Action<Worksheet>? Cleared;

    public event Action<Worksheet>? RowVisibilityChanged;

    private readonly OccupiedIndices _rowCounts = new();
    private readonly OccupiedIndices _columnCounts = new();

    private int _lastRow => _rowCounts.Last;
    private int _lastColumn => _columnCounts.Last;

    public IEnumerable<KeyValuePair<CellAddress, Cell>> CellsInRow(int row)
    {
        if (row > _lastRow) yield break;
        for (var column = 0; column <= _lastColumn; column++)
        {
            var address = new CellAddress(row, column);
            if (_cells.TryGetValue(address, out var cell)) yield return new(address, cell);
        }
    }

    public IEnumerable<int> RowsUsingColumn(int column)
    {
        if (column > _lastColumn) yield break;
        for (var row = 0; row <= _lastRow; row++)
            if (_cells.ContainsKey(new CellAddress(row, column))) yield return row;
    }

    public Workbook? Owner { get; internal set; }

    private void Store(CellAddress address, Cell value)
    {
        if (value.ExplicitStyle && value.StyleId == Styling.StyleCatalog.DefaultStyleId
            && Layout.DefaultStyleAt(address) == Styling.StyleCatalog.DefaultStyleId)
            value = value with { ExplicitStyle = false };
        if (value == Cell.Blank
            || (value.Value.IsEmpty && !value.HasFormula && !value.IsPartOfArrayFormula
                && !value.HasStyle && value.Hyperlink is null))
        {
            if (_cells.Remove(address)) Untrack(address);
        }
        else
        {
            if (!_cells.ContainsKey(address)) Track(address);
            _cells[address] = value;
        }
    }

    private void Track(CellAddress address)
    {
        _rowCounts.Add(address.Row);
        _columnCounts.Add(address.Column);
    }

    private void Untrack(CellAddress address)
    {
        _rowCounts.Remove(address.Row);
        _columnCounts.Remove(address.Column);
    }

    public CellValue GetValue(CellAddress address) => this[address].Value;

    public int StyleIdAt(CellAddress address) =>
        _cells.TryGetValue(address, out var cell) && cell.HasStyle ? cell.StyleId : Layout.DefaultStyleAt(address);

    private Cell Writable(CellAddress address) =>
        _cells.TryGetValue(address, out var cell) ? cell : Cell.Blank with { StyleId = Layout.DefaultStyleAt(address) };

    public void SetValue(CellAddress address, CellValue value) =>
        this[address] = Writable(address) with { Value = value, Formula = null, SpillRange = null, IsSpilled = false };

    public void SetStyleName(CellAddress address, string? name) =>
        this[address] = this[address] with { StyleName = name };

    public int? OwnStyleAt(CellAddress address) =>
        _cells.TryGetValue(address, out var cell) && cell.HasStyle ? cell.StyleId : null;

    public void SetStyle(CellAddress address, int styleId) =>
        this[address] = this[address] with { StyleId = styleId, ExplicitStyle = true };

    public void SetOwnStyle(CellAddress address, int? styleId) =>
        this[address] = this[address] with
        {
            StyleId = styleId ?? Styling.StyleCatalog.DefaultStyleId,
            ExplicitStyle = styleId is not null,
        };

    public void SetFormula(CellAddress address, string formula) =>
        this[address] = Writable(address) with { Formula = formula, ArrayFormulaRange = null, IsArrayAnchor = false, IsSpilled = false, ImplicitIntersection = false };

    public void SetArrayFormula(CellRange range, string formula)
    {
        foreach (var address in range.Cells())
            this[address] = Writable(address) with
            {
                Formula = address == range.Start ? formula : null,
                ArrayFormulaRange = range,
                IsArrayAnchor = address == range.Start,
            };
    }

    public bool IsBlank(CellAddress address) => !_cells.ContainsKey(address);

    public IEnumerable<CellAddress> CellAddresses => _cells.Keys;

    public int LastUsedRow => _lastRow;

    public int LastUsedColumn => _lastColumn;

    public int LastFormattedRow => Math.Max(_lastRow, BoundedStyleBlocks().Select(block => block.LastRow).DefaultIfEmpty(-1).Max());

    public int LastFormattedColumn => Math.Max(_lastColumn, BoundedStyleBlocks().Select(block => block.LastColumn).DefaultIfEmpty(-1).Max());

    private IEnumerable<StyleBlock> BoundedStyleBlocks() =>
        Layout.StyleBlocks.All.Where(block => block.LastRow < CellAddress.MaxRows - 1 && block.LastColumn < CellAddress.MaxColumns - 1);

    public bool TryGetCell(CellAddress address, out Cell cell) => _cells.TryGetValue(address, out cell!);

    public IEnumerable<KeyValuePair<CellAddress, Cell>> NonEmptyCells() =>
    _cells.OrderBy(kv => kv.Key);

    public IEnumerable<KeyValuePair<CellAddress, Cell>> NonEmptyCellsIn(CellRange range) =>
    _cells.Where(kv => range.Contains(kv.Key)).OrderBy(kv => kv.Key);

    public CellRange? UsedRange()
    {
        if (_cells.Count == 0) return null;
        int minR = int.MaxValue, minC = int.MaxValue, maxR = 0, maxC = 0;
        foreach (var a in _cells.Keys)
        {
            if (a.Row < minR) minR = a.Row;
            if (a.Row > maxR) maxR = a.Row;
            if (a.Column < minC) minC = a.Column;
            if (a.Column > maxC) maxC = a.Column;
        }
        return new CellRange(new CellAddress(minR, minC), new CellAddress(maxR, maxC));
    }

    internal void CopyCellsTo(Worksheet target)
    {
        target._cells = new Dictionary<CellAddress, Cell>(_cells);
        _rowCounts.CopyTo(target._rowCounts);
        _columnCounts.CopyTo(target._columnCounts);
    }

    public void Clear()
    {
        _cells.Clear();
        _rowCounts.Clear();
        _columnCounts.Clear();
        Revision++;
        Cleared?.Invoke(this);
    }
}
