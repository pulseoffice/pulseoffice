using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Pivot;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SheetCopy(Worksheet Sheet, IReadOnlyList<DefinedName> Names, IReadOnlyList<PivotDefinition> Pivots)
{
    public IReadOnlyList<CopiedTable> Tables { get; init; } = [];
}

public sealed record CopiedTable(DatabaseRange Table, bool CriteriaOnCopy);

public interface ISheetDuplicator
{
    SheetCopy Duplicate(Workbook workbook, Worksheet source, string name);

    void Insert(Workbook workbook, SheetCopy copy, int index);

    void Remove(Workbook workbook, SheetCopy copy);
}

public sealed class SheetDuplicator : ISheetDuplicator
{
    private readonly SheetTableCopies _tables = new();

    public SheetCopy Duplicate(Workbook workbook, Worksheet source, string name)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var copy = Sheet(source, name);
        new ChartSheetRewrite(source.Name, name).Rewrite(copy);
        var tables = _tables.Copies(workbook, workbook.IndexOf(source));
        _tables.Rename(copy, tables);
        return new SheetCopy(copy, Names(workbook, source.Name, name), Pivots(workbook, source.Name, name)) { Tables = [.. tables.Values] };
    }

    public void Insert(Workbook workbook, SheetCopy copy, int index)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(copy);

        workbook.InsertSheet(copy.Sheet, index);
        foreach (var name in copy.Names) workbook.DefineName(name with { Scope = copy.Sheet.Name });
        _tables.Define(workbook, copy);
        foreach (var pivot in copy.Pivots.Where(pivot => !workbook.Pivots.Contains(pivot))) workbook.Pivots.Add(pivot);
    }

    public void Remove(Workbook workbook, SheetCopy copy)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(copy);

        foreach (var pivot in copy.Pivots) workbook.Pivots.Remove(pivot);
        workbook.RemoveSheet(copy.Sheet);
    }

    private List<DefinedName> Names(Workbook workbook, string sourceName, string copyName)
    {
        var local = workbook.DefinedNames.Where(name => string.Equals(name.Scope, sourceName, StringComparison.OrdinalIgnoreCase)).ToList();
        var taken = local.Select(name => name.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var global = workbook.DefinedNames
            .Where(name => name.Scope is null && !taken.Contains(name.Name) && Rebased(name.Expression, sourceName, copyName) is not null);

        return [.. local.Concat(global).Select(name => name with
        {
            Scope = copyName,
            Expression = Rebased(name.Expression, sourceName, copyName) ?? name.Expression,
        })];
    }

    private string? Rebased(string expression, string sourceName, string copyName)
    {
        var rewriter = new SheetNameRewriter(sourceName, copyName);
        var rewritten = rewriter.Rewrite(expression.StartsWith('=') ? expression : "=" + expression);
        if (!rewriter.Changed || rewritten is null) return null;
        return expression.StartsWith('=') ? rewritten : rewritten[1..];
    }

    private List<PivotDefinition> Pivots(Workbook workbook, string sourceName, string copyName)
    {
        var names = workbook.Pivots.Select(pivot => pivot.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copies = new List<PivotDefinition>();
        foreach (var pivot in workbook.Pivots.Where(pivot => string.Equals(pivot.DestinationSheet, sourceName, StringComparison.OrdinalIgnoreCase)))
        {
            var fresh = Unique(pivot.Name, names);
            names.Add(fresh);
            copies.Add(pivot with
            {
                Id = NewId(),
                Name = fresh,
                DestinationSheet = copyName,
                SourceSheet = string.Equals(pivot.SourceSheet, sourceName, StringComparison.OrdinalIgnoreCase) ? copyName : pivot.SourceSheet,
            });
        }
        return copies;
    }

    private string Unique(string name, HashSet<string> taken)
    {
        for (var suffix = 2; ; suffix++)
            if (!taken.Contains($"{name} {suffix}")) return $"{name} {suffix}";
    }

    private Worksheet Sheet(Worksheet source, string name)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var copy = new Worksheet(name)
        {
            TabColorHex = source.TabColorHex,
            RightToLeft = source.RightToLeft,
            PageStyleName = source.PageStyleName,
            RepeatedPrintRows = source.RepeatedPrintRows,
            RepeatedPrintColumns = source.RepeatedPrintColumns,
            Protection = source.Protection,
        };

        source.Layout.CopyTo(copy.Layout);

        foreach (var (address, cell) in source.NonEmptyCells()) copy[address] = cell;

        copy.Merges.AddRange(source.Merges.All);
        copy.Sparklines.AddRange(source.Sparklines.Groups);
        copy.ConditionalRules.AddRange(source.ConditionalRules);
        copy.Validations.AddRange(source.Validations);
        copy.PrintRanges.AddRange(source.PrintRanges);
        copy.Scenarios.AddRange(source.Scenarios);
        copy.RowGroups.AddRange(source.RowGroups);
        copy.ColumnGroups.AddRange(source.ColumnGroups);
        copy.TraceArrows.AddRange(source.TraceArrows);
        copy.RowBreaks.UnionWith(source.RowBreaks);
        copy.ColumnBreaks.UnionWith(source.ColumnBreaks);

        foreach (var (address, comment) in source.Comments) copy.Comments[address] = comment;
        foreach (var label in source.Labels.All) copy.Labels.Add(label);
        foreach (var (kind, macro) in source.Events.All) copy.Events.Bind(kind, macro);

        copy.AutoFilter.Restore(source.AutoFilter.Save());
        copy.RangeFilter = source.RangeFilter;
        copy.RangeSort = source.RangeSort;

        copy.Objects.AddRange(Objects(source.Objects));

        return copy;
    }

    private IEnumerable<PlacedObject> Objects(IReadOnlyList<PlacedObject> objects)
    {
        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var placed in objects)
        {
            var fresh = NewId();
            renamed[placed.Id] = fresh;
            renamed.TryAdd(placed.Content.Id, fresh);
        }

        return objects.Select(placed => placed with
        {
            Id = renamed[placed.Id],
            Content = Remapped(placed.Content, renamed) with { Id = renamed[placed.Id] },
        }).ToList();
    }

    private EmbeddedObject Remapped(EmbeddedObject content, IReadOnlyDictionary<string, string> renamed) =>
        content is ConnectorObject connector
            ? connector with
            {
                Start = Endpoint(connector.Start, renamed),
                End = Endpoint(connector.End, renamed),
            }
            : content;

    private ConnectorEndpoint Endpoint(ConnectorEndpoint endpoint, IReadOnlyDictionary<string, string> renamed) =>
        endpoint.ObjectId is { } id && renamed.TryGetValue(id, out var fresh)
            ? endpoint with { ObjectId = fresh }
            : endpoint;

    private string NewId() => Guid.NewGuid().ToString("N");
}
