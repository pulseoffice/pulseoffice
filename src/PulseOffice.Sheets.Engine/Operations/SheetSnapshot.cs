using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SheetSnapshot(
    IReadOnlyList<(CellAddress Address, Cell Cell)> Cells,
    SheetLayout Layout,
    IReadOnlyList<PlacedObject> Objects,
    IReadOnlyList<Formatting.ConditionalRule> Rules,
    IReadOnlyList<CellRange> Merges,
    IReadOnlyList<SparklineGroup> Sparklines)
{
    public IReadOnlyList<(CellAddress Address, CellComment Comment)> Comments { get; init; } = [];

    public IReadOnlyList<CellRange> PrintRanges { get; init; } = [];

    public IReadOnlyList<OutlineGroup> RowGroups { get; init; } = [];

    public IReadOnlyList<OutlineGroup> ColumnGroups { get; init; } = [];

    public OutlineSummary OutlineSummary { get; init; } = OutlineSummary.Default;

    public ConsolidationSettings? Consolidation { get; init; }

    public IReadOnlyList<DataValidation> Validations { get; init; } = [];

    public IReadOnlyList<int> RowBreaks { get; init; } = [];

    public IReadOnlyList<int> ColumnBreaks { get; init; } = [];

    public IReadOnlyList<(Worksheet Sheet, CellAddress Address, string Formula)> OtherFormulas { get; init; } = [];

    public AutoFilterMemento? AutoFilter { get; init; }

    public IReadOnlyList<(Worksheet Sheet, SheetRangeFilter? Filter)> RangeFilters { get; init; } = [];

    public SheetRangeSort? RangeSort { get; init; }

    public IReadOnlyList<Scenario> Scenarios { get; init; } = [];

    public IReadOnlyList<QueryBinding> QueryBindings { get; init; } = [];

    public IReadOnlyList<LabelRange> Labels { get; init; } = [];

    public FrozenPanes Frozen { get; init; } = FrozenPanes.None;
}

public interface ISheetSnapshots
{
    SheetSnapshot Capture(Worksheet sheet);

    void Restore(CalculationEngine engine, int sheetIndex, SheetSnapshot snapshot);
}

public sealed class SheetSnapshots : ISheetSnapshots
{
    public SheetSnapshot Capture(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return new SheetSnapshot(
            [.. sheet.NonEmptyCells().Select(pair => (pair.Key, pair.Value))],
            LayoutCopy(sheet.Layout),
            [.. sheet.Objects],
            [.. sheet.ConditionalRules],
            [.. sheet.Merges.All],
            [.. sheet.Sparklines.Groups])
        {
            Comments = [.. sheet.Comments.Select(pair => (pair.Key, pair.Value))],
            PrintRanges = [.. sheet.PrintRanges],
            RowGroups = [.. sheet.RowGroups],
            ColumnGroups = [.. sheet.ColumnGroups],
            OutlineSummary = sheet.OutlineSummary,
            Consolidation = sheet.Owner?.Consolidation,
            Validations = [.. sheet.Validations],
            RowBreaks = [.. sheet.RowBreaks],
            ColumnBreaks = [.. sheet.ColumnBreaks],
            OtherFormulas = [.. (sheet.Owner?.Sheets ?? [])
                .Where(other => !ReferenceEquals(other, sheet))
                .SelectMany(other => other.NonEmptyCells()
                    .Where(pair => pair.Value.HasFormula)
                    .Select(pair => (other, pair.Key, pair.Value.Formula!)))],
            AutoFilter = sheet.AutoFilter.Save(),
            RangeFilters = [.. (sheet.Owner?.Sheets ?? [sheet]).Select(owner => (owner, owner.RangeFilter))],
            RangeSort = sheet.RangeSort,
            Scenarios = [.. sheet.Scenarios],
            QueryBindings = [.. sheet.QueryBindings.All],
            Labels = [.. sheet.Labels.All],
            Frozen = sheet.Frozen,
        };
    }

    private SheetLayout LayoutCopy(SheetLayout layout)
    {
        var copy = new SheetLayout();
        layout.CopyTo(copy);
        return copy;
    }

    public void Restore(CalculationEngine engine, int sheetIndex, SheetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(snapshot);

        var sheet = engine.Workbook.Sheets[sheetIndex];

        sheet.Clear();

        snapshot.Layout.CopyTo(sheet.Layout);

        foreach (var (address, cell) in snapshot.Cells) sheet[address] = cell;
        foreach (var (anchor, cell) in snapshot.Cells)
        {
            if (cell.SpillRange is not { } spill) continue;
            for (var row = spill.Start.Row; row <= spill.End.Row; row++)
                for (var column = spill.Start.Column; column <= spill.End.Column; column++)
                {
                    var address = new CellAddress(row, column);
                    if (address == anchor || !sheet.TryGetCell(address, out var spilled) || spilled.HasFormula) continue;
                    sheet[address] = spilled with { IsSpilled = true };
                }
        }

        sheet.Objects.Clear();
        sheet.Objects.AddRange(snapshot.Objects);

        sheet.ConditionalRules.Clear();
        sheet.ConditionalRules.AddRange(snapshot.Rules);

        sheet.Merges.Clear();
        sheet.Merges.AddRange(snapshot.Merges);

        sheet.Sparklines.Clear();
        sheet.Sparklines.AddRange(snapshot.Sparklines);

        sheet.Comments.Clear();
        foreach (var (address, comment) in snapshot.Comments) sheet.Comments[address] = comment;

        Replace(sheet.PrintRanges, snapshot.PrintRanges);
        Replace(sheet.RowGroups, snapshot.RowGroups);
        Replace(sheet.ColumnGroups, snapshot.ColumnGroups);
        sheet.OutlineSummary = snapshot.OutlineSummary;
        Replace(sheet.Validations, snapshot.Validations);

        sheet.RowBreaks.Clear();
        sheet.RowBreaks.UnionWith(snapshot.RowBreaks);
        sheet.ColumnBreaks.Clear();
        sheet.ColumnBreaks.UnionWith(snapshot.ColumnBreaks);

        Replace(sheet.Scenarios, snapshot.Scenarios);
        foreach (var binding in sheet.QueryBindings.All.ToList()) sheet.QueryBindings.Remove(binding.Id);
        foreach (var binding in snapshot.QueryBindings) sheet.QueryBindings.AddOrUpdate(binding);
        sheet.Labels.Clear();
        foreach (var label in snapshot.Labels) sheet.Labels.Add(label);
        sheet.Frozen = snapshot.Frozen;

        if (snapshot.AutoFilter is { } autoFilter) sheet.AutoFilter.Restore(autoFilter);
        foreach (var (owner, filter) in snapshot.RangeFilters) owner.RangeFilter = filter;
        sheet.RangeSort = snapshot.RangeSort;
        if (sheet.Owner is { } book) book.Consolidation = snapshot.Consolidation;

        foreach (var (other, address, formula) in snapshot.OtherFormulas)
            if (other[address].Formula != formula) other[address] = other[address] with { Formula = formula };

        engine.RecalculateAll();
    }

    private void Replace<T>(List<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        target.AddRange(source);
    }
}
