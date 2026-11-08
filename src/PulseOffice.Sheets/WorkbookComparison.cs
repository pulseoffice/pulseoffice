using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public enum SheetDifferenceKind { Added, Removed }

public sealed record SheetDifference(SheetDifferenceKind Kind, string SheetName);

public sealed record WorkbookDifferences
{
    public IReadOnlyList<TrackedChange> CellChanges { get; init; } = [];

    public IReadOnlyList<SheetDifference> SheetChanges { get; init; } = [];

    public bool IsEmpty => CellChanges.Count == 0 && SheetChanges.Count == 0;
}

public interface IWorkbookComparer
{
    WorkbookDifferences Compare(Workbook mine, Workbook theirs, string? author = null);
}

public sealed class WorkbookComparer : IWorkbookComparer
{
    public WorkbookDifferences Compare(Workbook mine, Workbook theirs, string? author = null)
    {
        ArgumentNullException.ThrowIfNull(mine);
        ArgumentNullException.ThrowIfNull(theirs);

        var cells = new List<TrackedChange>();
        var sheets = new List<SheetDifference>();
        var moment = DateTimeOffset.Now;

        foreach (var theirSheet in theirs.Sheets)
        {
            var index = IndexOf(mine, theirSheet.Name);

            if (index < 0)
            {
                sheets.Add(new SheetDifference(SheetDifferenceKind.Added, theirSheet.Name));
                continue;
            }

            cells.AddRange(CompareSheets(mine.Sheets[index], theirSheet, index, author, moment));
        }

        foreach (var mySheet in mine.Sheets)
            if (IndexOf(theirs, mySheet.Name) < 0)
                sheets.Add(new SheetDifference(SheetDifferenceKind.Removed, mySheet.Name));

        return new WorkbookDifferences { CellChanges = cells, SheetChanges = sheets };
    }

    private static IEnumerable<TrackedChange> CompareSheets(
        Worksheet mine, Worksheet theirs, int sheetIndex, string? author, DateTimeOffset moment)
    {
        var addresses = new SortedSet<CellAddress>();

        foreach (var pair in mine.NonEmptyCells()) addresses.Add(pair.Key);
        foreach (var pair in theirs.NonEmptyCells()) addresses.Add(pair.Key);

        foreach (var address in addresses)
        {
            var before = mine[address];
            var after = theirs[address];

            if (Same(before, after)) continue;

            yield return new TrackedChange
            {
                Id = Guid.NewGuid().ToString("N"),
                SheetIndex = sheetIndex,
                Address = address,
                Before = before.Value,
                After = after.Value,
                FormulaBefore = before.Formula,
                FormulaAfter = after.Formula,
                Author = author,
                At = moment,
            };
        }
    }

    private static bool Same(Cell mine, Cell theirs) =>
        mine.Formula == theirs.Formula && Equals(mine.Value, theirs.Value);

    private static int IndexOf(Workbook workbook, string name)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (string.Equals(workbook.Sheets[index].Name, name, StringComparison.OrdinalIgnoreCase))
                return index;

        return -1;
    }
}

public interface IWorkbookMerger
{
    int Merge(Workbook target, WorkbookDifferences differences);
}

public sealed class WorkbookMerger : IWorkbookMerger
{
    public int Merge(Workbook target, WorkbookDifferences differences)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(differences);

        foreach (var difference in differences.SheetChanges)
            if (difference.Kind == SheetDifferenceKind.Added
                && target.FindSheet(difference.SheetName) is null)
                target.AddSheet(difference.SheetName);

        target.Changes.Import(differences.CellChanges);

        return differences.CellChanges.Count + differences.SheetChanges.Count;
    }
}
