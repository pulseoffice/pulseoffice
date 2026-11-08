using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public sealed class CachedPivotSource(PivotRecordCache cache) : IPivotSource
{
    public IReadOnlyList<string> Headers => cache.Headers;

    public int RowCount => cache.Records.Count;

    public CellValue ValueAt(int row, int column) =>
        row >= 0 && row < cache.Records.Count && column >= 0 && column < cache.Records[row].Count
            ? cache.Records[row][column]
            : CellValue.Empty;
}

public interface IPivotSources
{
    IPivotSource? Open(Workbook workbook, PivotDefinition definition);

    long RevisionOf(Workbook workbook, PivotDefinition definition);
}

public sealed class PivotSources : IPivotSources
{
    public IPivotSource? Open(Workbook workbook, PivotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.ExternalSource is { } external)
            return external.Cache.HasRecords ? new CachedPivotSource(external.Cache) : null;

        return workbook.FindSheet(definition.SourceSheet) is { } sheet ? new RangePivotSource(sheet, definition.SourceRange) : null;
    }

    public long RevisionOf(Workbook workbook, PivotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.IsExternal) return 0;
        return workbook.FindSheet(definition.SourceSheet)?.Revision ?? -1;
    }
}
