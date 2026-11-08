using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.Pivot;

public interface IPivotCoordinator
{
    OperationResult<CellRange> Apply(Workbook workbook, PivotDefinition definition, PivotLabels labels);

    OperationResult<int> RefreshAll(Workbook workbook, PivotLabels labels);

    void Remove(Workbook workbook, string id);
}

public sealed class PivotCoordinator(IPivotEngine engine, IPivotRenderer renderer, IPivotSources? sources = null) : IPivotCoordinator
{
    private readonly IPivotSources _sources = sources ?? new PivotSources();

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

        var destination = workbook.FindSheet(definition.DestinationSheet)
            ?? workbook.AddSheet(definition.DestinationSheet);

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

        if (existing is null
            && renderer.Measure(table, definition).Cells().Any(address => !destination[address].Value.IsEmpty || destination[address].HasFormula))
            return OperationResult<CellRange>.Fail(Diagnostic.Error("PVT005", "The destination already contains data.", definition.DestinationSheet));
        if (existing?.OutputRange is { } previous) Clear(destination, previous);

        var written = renderer.Render(table, definition, destination, workbook.Styles, labels);

        var stored = definition with { OutputRange = written };

        if (existing is null) workbook.Pivots.Add(stored);
        else workbook.Pivots[workbook.Pivots.IndexOf(existing)] = stored;

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
