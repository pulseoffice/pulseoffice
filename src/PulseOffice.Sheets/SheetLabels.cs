using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum LabelOrientation { Rows, Columns }

public sealed record LabelRange(CellRange Labels, CellRange Data, LabelOrientation Orientation);

public sealed class LabelRangeCollection
{
    private readonly List<LabelRange> _ranges = [];

    public IReadOnlyList<LabelRange> All => _ranges;

    public void Add(LabelRange range)
    {
        ArgumentNullException.ThrowIfNull(range);

        _ranges.RemoveAll(existing => existing.Labels == range.Labels);
        _ranges.Add(range);
    }

    public void Clear() => _ranges.Clear();

    public bool Remove(CellRange labels) => _ranges.RemoveAll(range => range.Labels == labels) > 0;

    public LabelRange? For(CellAddress address) =>
        _ranges.FirstOrDefault(range => range.Labels.Contains(address));
}

public enum SheetEventKind
{
    Activated,
    Deactivated,
    ContentChanged,
    SelectionChanged,
    DoubleClicked,
    RightClicked,
    Calculated,
}

public sealed class SheetEventBindings
{
    private readonly Dictionary<SheetEventKind, string> _macros = [];

    public IReadOnlyDictionary<SheetEventKind, string> All => _macros;

    public string? For(SheetEventKind kind) => _macros.GetValueOrDefault(kind);

    public void Bind(SheetEventKind kind, string? macro)
    {
        if (string.IsNullOrWhiteSpace(macro)) _macros.Remove(kind);
        else _macros[kind] = macro.Trim();
    }
}
