using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum SortOrientation
{
    TopToBottom,
    LeftToRight,
}

public sealed record SortField(int ColumnOffset, bool Ascending = true)
{
    public IReadOnlyList<string>? CustomOrder { get; init; }
}

public sealed record SortSettings
{
    public IReadOnlyList<SortField> Fields { get; init; } = [];

    public bool CaseSensitive { get; init; }

    public SortOrientation Orientation { get; init; }

    public bool LeftToRight => Orientation == SortOrientation.LeftToRight;

    public SortSettings? Moved(Func<int, int?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var kept = Fields
            .Select(field => fields(field.ColumnOffset) is { } offset ? field with { ColumnOffset = offset } : null)
            .OfType<SortField>()
            .ToList();
        return kept.Count == 0 ? null : this with { Fields = kept };
    }

    public bool FitsWithin(int columns) => Fields.Count > 0 && Fields.All(field => field.ColumnOffset >= 0 && field.ColumnOffset < columns);

    public bool FitsWithin(CellRange range) => FitsWithin(LeftToRight ? range.RowCount : range.ColumnCount);
}

public sealed record SheetRangeSort(CellRange Range, bool HasHeader, SortSettings Settings)
{
    public CellRange Data => !HasHeader ? Range
        : Settings.LeftToRight
            ? Range.ColumnCount > 1 ? new CellRange(new CellAddress(Range.Start.Row, Range.Start.Column + 1), Range.End) : Range
            : Range.RowCount > 1 ? new CellRange(new CellAddress(Range.Start.Row + 1, Range.Start.Column), Range.End) : Range;
}
