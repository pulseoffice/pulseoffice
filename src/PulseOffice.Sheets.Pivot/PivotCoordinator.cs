using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.Pivot;

public interface IPivotCoordinator
{
    OperationResult<CellRange> Apply(Workbook workbook, PivotDefinition definition, PivotLabels labels);

    OperationResult<int> RefreshAll(Workbook workbook, PivotLabels labels);

    void Remove(Workbook workbook, string id);

    int FollowCharts(Workbook workbook);
}

public sealed class PivotCoordinator(IPivotEngine engine, IPivotRenderer renderer, IPivotSources? sources = null, IPivotChartLinks? charts = null) : IPivotCoordinator
{
    private readonly IPivotSources _sources = sources ?? new PivotSources();
    private readonly IPivotChartLinks _charts = charts ?? new PivotChartLinks();

    public OperationResult<CellRange> Apply(Workbook workbook, PivotDefinition definition, PivotLabels labels)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.IsValid)
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT001", "The pivot table has no value fields."));

        if (definition.ExternalSource is { Cache.HasRecords: false })
            return KeepLastResult(workbook, definition);

        var source = definition.IsExternal ? null : workbook.FindSheet(definition.SourceSheet);
        if (!definition.IsExternal && source is null)
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT002", $"Source sheet '{definition.SourceSheet}' does not exist.", definition.SourceSheet).With(definition.SourceSheet));

        var destination = workbook.FindSheet(definition.DestinationSheet);
        if (destination is null && workbook.Protection.Structure)
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT007", "The workbook structure is protected: the destination sheet cannot be added.", definition.DestinationSheet).With(definition.DestinationSheet));

        if (ReferenceEquals(source, destination) && definition.SourceRange.Contains(definition.Destination))
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT003", "The destination lies inside the source data."));

        var table = engine.Build(definition, _sources.Open(workbook, definition)!);

        var extent = renderer.Measure(table, definition);
        if (!extent.End.IsValid)
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT006", "The pivot table does not fit in the destination sheet.", definition.DestinationSheet));

        if (ReferenceEquals(source, destination)
            && renderer.Measure(table, definition).IntersectsWith(definition.SourceRange))
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT004", "The pivot table would overlap the source data."));

        var existing = workbook.Pivots.FirstOrDefault(pivot => pivot.Id == definition.Id);

        var previousSheet = existing is null ? null : workbook.FindSheet(existing.DestinationSheet);
        var previous = previousSheet is null ? null : existing!.OutputRange;

        bool OwnedByThisPivot(CellAddress address) =>
            previous is { } owned && ReferenceEquals(previousSheet, destination) && owned.Contains(address);

        if (destination is not null && extent.Cells().Any(address => !OwnedByThisPivot(address) && (!destination[address].Value.IsEmpty || destination[address].HasFormula)))
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT005", "The destination already contains data.", definition.DestinationSheet));
        if (previous is { } range) Clear(previousSheet!, range);

        destination ??= workbook.AddSheet(definition.DestinationSheet);

        var written = renderer.Render(table, definition, destination, workbook.Styles, labels);

        var stored = definition with { OutputRange = written };

        if (existing is null) workbook.Pivots.Add(stored);
        else workbook.Pivots[workbook.Pivots.IndexOf(existing)] = stored;

        _charts.Follow(workbook, existing?.Name ?? stored.Name, stored, table);

        return OperationResult<CellRange>.Ok(written);
    }

    public OperationResult<int> RefreshAll(Workbook workbook, PivotLabels labels)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var refreshed = 0;
        var problems = new List<Diagnostic>();

        foreach (var definition in workbook.Pivots.ToList())
        {
            var result = Apply(workbook, definition, labels);

            if (result.Succeeded) refreshed++;
            else problems.AddRange(result.Diagnostics);
        }

        return problems.Count == 0
            ? OperationResult<int>.Ok(refreshed)
            : OperationResult<int>.Fail([.. problems]);
    }

    public int FollowCharts(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var followed = 0;
        foreach (var definition in workbook.Pivots.ToList())
        {
            if (!definition.IsValid || !Linked(workbook, definition.Name)) continue;
            if (definition.ExternalSource is { Cache.HasRecords: false }) continue;
            if (_sources.Open(workbook, definition) is not { } source) continue;
            followed += _charts.Follow(workbook, definition.Name, definition, engine.Build(definition, source));
        }
        return followed;
    }

    private static bool Linked(Workbook workbook, string name) =>
        workbook.Sheets.SelectMany(sheet => sheet.Objects).Any(placed =>
            placed.Content is PulseOffice.Charts.ChartDefinition { PivotSource: { } link } && string.Equals(link.PivotName, name, StringComparison.OrdinalIgnoreCase));

    private OperationResult<CellRange> KeepLastResult(Workbook workbook, PivotDefinition definition)
    {
        var range = definition.OutputRange ?? new CellRange(definition.Destination, definition.Destination);
        var existing = workbook.Pivots.FirstOrDefault(pivot => pivot.Id == definition.Id);
        var stored = definition with { OutputRange = range };
        if (existing is null) workbook.Pivots.Add(stored);
        else workbook.Pivots[workbook.Pivots.IndexOf(existing)] = stored;
        return OperationResult<CellRange>.Ok(range);
    }

    public void Remove(Workbook workbook, string id)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (workbook.Pivots.FirstOrDefault(pivot => pivot.Id == id) is not { } definition) return;

        _charts.Detach(workbook, definition);

        if (definition.OutputRange is { } range
            && workbook.FindSheet(definition.DestinationSheet) is { } sheet)
            Clear(sheet, range);

        workbook.Pivots.Remove(definition);
    }

    private static void Clear(Worksheet sheet, CellRange range)
    {
        foreach (var address in range.Cells())
        {
            sheet.SetValue(address, Values.CellValue.Empty);
            sheet.SetStyle(address, StyleCatalog.DefaultStyleId);
        }
    }
}
