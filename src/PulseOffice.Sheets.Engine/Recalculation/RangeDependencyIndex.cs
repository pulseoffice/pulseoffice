using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Recalculation;

public sealed class RangeDependencyIndex
{
    private const int ColumnBlock = 64;
    private const int RowBlock = 1024;
    private const int MaxRowBlocksPerEntry = 16;
    private const int MaxColumnBlocksPerEntry = 16;

    private readonly record struct Entry(SheetCell Dependent, CellRange Range);

    private readonly record struct Bucket(int SheetIndex, int ColumnBlock, int RowBlock);

    private const int AnyRow = -1;
    private const int AnyColumn = -1;

    private readonly Dictionary<Bucket, List<Entry>> _buckets = [];
    private readonly Dictionary<int, List<Entry>> _sheetWide = [];

    public void Add(SheetCell dependent, int sheetIndex, CellRange range)
    {
        var entry = new Entry(dependent, range);
        foreach (var bucket in BucketsOf(sheetIndex, range))
            List(bucket).Add(entry);
        if (IsSheetWide(range)) SheetWide(sheetIndex).Add(entry);
    }

    public void Remove(SheetCell dependent, int sheetIndex, CellRange range)
    {
        var entry = new Entry(dependent, range);
        if (IsSheetWide(range))
        {
            if (_sheetWide.TryGetValue(sheetIndex, out var wide)) Drop(wide, entry, () => _sheetWide.Remove(sheetIndex));
            return;
        }
        foreach (var bucket in BucketsOf(sheetIndex, range))
            if (_buckets.TryGetValue(bucket, out var list)) Drop(list, entry, () => _buckets.Remove(bucket));
    }

    public void CollectDependents(SheetCell cell, ISet<SheetCell> result)
    {
        var columnBlock = cell.Address.Column / ColumnBlock;
        var rowBlock = cell.Address.Row / RowBlock;
        Scan(new Bucket(cell.SheetIndex, columnBlock, rowBlock), cell, result);
        Scan(new Bucket(cell.SheetIndex, columnBlock, AnyRow), cell, result);
        Scan(new Bucket(cell.SheetIndex, AnyColumn, rowBlock), cell, result);
        if (_sheetWide.TryGetValue(cell.SheetIndex, out var wide))
            foreach (var entry in wide)
                if (entry.Range.Contains(cell.Address)) result.Add(entry.Dependent);
    }

    public void Clear()
    {
        _buckets.Clear();
        _sheetWide.Clear();
    }

    private void Scan(Bucket bucket, SheetCell cell, ISet<SheetCell> result)
    {
        if (!_buckets.TryGetValue(bucket, out var list)) return;
        foreach (var entry in list)
            if (entry.Range.Contains(cell.Address)) result.Add(entry.Dependent);
    }

    private IEnumerable<Bucket> BucketsOf(int sheetIndex, CellRange range)
    {
        var firstColumn = range.Start.Column / ColumnBlock;
        var lastColumn = range.End.Column / ColumnBlock;
        var firstRow = range.Start.Row / RowBlock;
        var lastRow = range.End.Row / RowBlock;
        var columnBlocks = lastColumn - firstColumn + 1;
        var rowBlocks = lastRow - firstRow + 1;

        if (rowBlocks <= MaxRowBlocksPerEntry && columnBlocks <= MaxColumnBlocksPerEntry)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
                for (var row = firstRow; row <= lastRow; row++)
                    yield return new Bucket(sheetIndex, column, row);
        }
        else if (columnBlocks <= MaxColumnBlocksPerEntry)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
                yield return new Bucket(sheetIndex, column, AnyRow);
        }
        else if (rowBlocks <= MaxRowBlocksPerEntry)
        {
            for (var row = firstRow; row <= lastRow; row++)
                yield return new Bucket(sheetIndex, AnyColumn, row);
        }
    }

    private bool IsSheetWide(CellRange range) =>
        range.End.Column / ColumnBlock - range.Start.Column / ColumnBlock + 1 > MaxColumnBlocksPerEntry
        && range.End.Row / RowBlock - range.Start.Row / RowBlock + 1 > MaxRowBlocksPerEntry;

    private List<Entry> List(Bucket bucket)
    {
        if (!_buckets.TryGetValue(bucket, out var list)) _buckets[bucket] = list = [];
        return list;
    }

    private List<Entry> SheetWide(int sheetIndex)
    {
        if (!_sheetWide.TryGetValue(sheetIndex, out var list)) _sheetWide[sheetIndex] = list = [];
        return list;
    }

    private void Drop(List<Entry> list, Entry entry, Action whenEmpty)
    {
        var index = list.IndexOf(entry);
        if (index < 0) return;
        list[index] = list[^1];
        list.RemoveAt(list.Count - 1);
        if (list.Count == 0) whenEmpty();
    }
}
