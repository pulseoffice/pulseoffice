using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class PivotFacade(
    IScriptDispatcher dispatcher,
    IActiveSheetContext context,
    IPivotCoordinator coordinator,
    PulseOffice.Localization.ITextCatalog texts,
    PulseOffice.Sheets.Engine.Operations.IWorkbookCheckpoints checkpoints,
    IScriptReferences references,
    IScriptProtection protection) : ScriptFacade(dispatcher)
{
    private PivotLabels Labels => new(texts["pivot.grandTotal"], texts["pivot.subtotal"]);

    public string[] Names => Read(() =>
        context.Workbook is { } workbook
            ? [.. workbook.Pivots.Select(pivot => pivot.Name)]
            : Array.Empty<string>());

    public int Count => Read(() => context.Workbook?.Pivots.Count ?? 0);

    public string Create(
        string name,
        string sourceRange,
        string destination,
        string[] rowFields,
        string[] columnFields,
        string[] valueFields,
        string aggregation = "Sum") => Write(() => Checkpointed("Pivot", () =>
    {
        var source = references.Range(sourceRange);
        var at = references.Address(destination);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The pivot table name is empty.", nameof(name));
        protection.Unprotected("creating a pivot table");
        if (context.Workbook is not { } workbook) return string.Empty;
        if (workbook.Pivots.Any(pivot => string.Equals(pivot.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            throw new ArgumentException($"There is already a pivot table named '{name}'.", nameof(name));

        var sheet = workbook.Sheets[context.SheetIndex];

        var headers = Headers(sheet, source);

        if (!Enum.TryParse<PivotAggregation>(aggregation, ignoreCase: true, out var how) || !Enum.IsDefined(how))
            throw new ArgumentException($"Unknown aggregation '{aggregation}'.", nameof(aggregation));

        var unknown = (rowFields ?? []).Concat(columnFields ?? []).Concat(valueFields ?? [])
            .Where(field => !headers.Any(header => string.Equals(header.Header, field, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
        if (unknown.Count > 0)
            throw new ArgumentException(
                $"The source has no column {string.Join(", ", unknown.Select(field => $"'{field}'"))}: its headers are {string.Join(", ", headers.Select(header => header.Header))}.");
        if (valueFields is not { Length: > 0 })
            throw new ArgumentException("A pivot table needs at least one value field.", nameof(valueFields));

        var fields = new List<PivotField>();

        foreach (var (header, index) in headers)
        {
            var area = Area(header, rowFields, columnFields, valueFields);

            if (area == PivotArea.Unused) continue;

            fields.Add(new PivotField
            {
                SourceName = header,
                SourceColumnIndex = index,
                Area = area,
                Aggregation = how,
            });
        }


        var definition = new PivotDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            SourceSheet = sheet.Name,
            SourceRange = source,
            Destination = at,
            DestinationSheet = sheet.Name,
            Fields = fields,
        };

        var applied = coordinator.Apply(workbook, definition, Labels);

        if (!applied.Succeeded) throw new InvalidOperationException($"The pivot table could not be placed at {at}: {string.Join(" ", applied.Diagnostics.Select(diagnostic => diagnostic.Message))}");

        context.RequestRefresh();

        return definition.Id;
    }, id => id.Length > 0));

    public int Refresh() => Write(() => Checkpointed("Pivot", () =>
    {
        if (context.Workbook is not { } workbook) return 0;
        protection.Unprotected("refreshing pivot tables");

        var refreshed = coordinator.RefreshAll(workbook, Labels);

        context.RequestRefresh();

        return refreshed.Succeeded ? refreshed.Value : 0;
    }, count => count > 0));

    public bool Remove(string name) => Write(() => Checkpointed("Pivot", () =>
    {
        if (context.Workbook is not { } workbook) return false;

        if (workbook.Pivots.FirstOrDefault(pivot =>
                string.Equals(pivot.Name, name, StringComparison.CurrentCultureIgnoreCase)
                || pivot.Id == name) is not { } found)
            return false;
        protection.Unprotected("removing a pivot table");

        coordinator.Remove(workbook, found.Id);
        workbook.Pivots.Remove(found);

        context.RequestRefresh();

        return true;
    }, done => done));

    private T Checkpointed<T>(string label, Func<T> change, Func<T, bool> applied)
    {
        if (context.Workbook is not { } workbook || context.Engine is not { } engine) return change();

        var before = checkpoints.Capture(workbook);
        var result = change();
        if (!applied(result)) return result;

        var after = checkpoints.Capture(workbook);
        context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            label,
            redo: () => { checkpoints.Restore(engine, workbook, after); context.RequestRefresh(); },
            undo: () => { checkpoints.Restore(engine, workbook, before); context.RequestRefresh(); }));

        return result;
    }

    private IReadOnlyList<(string Header, int Index)> Headers(Worksheet sheet, CellRange source)
    {
        var headers = new List<(string, int)>();

        for (var column = source.Start.Column; column <= source.End.Column; column++)
        {
            var text = sheet[new CellAddress(source.Start.Row, column)].Value.ToString() ?? string.Empty;

            headers.Add((text, column - source.Start.Column));
        }

        return headers;
    }

    private PivotArea Area(string header, string[]? rows, string[]? columns, string[]? values)
    {
        if (Contains(rows, header)) return PivotArea.Row;
        if (Contains(columns, header)) return PivotArea.Column;
        if (Contains(values, header)) return PivotArea.Value;

        return PivotArea.Unused;
    }

    private bool Contains(string[]? names, string header) =>
        names is not null && names.Any(name =>
            string.Equals(name, header, StringComparison.CurrentCultureIgnoreCase));
}
