using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal abstract class ChartReferenceRewrite
{
    private readonly ChartSourceReferences _references = new();

    protected IChartSourceReferences References => _references;

    public ChartDefinition Rewrite(ChartDefinition chart)
    {
        if (chart.InlineData is not null) return chart;

        var sheet = chart.SourceSheet;
        var range = chart.SourceRange;
        if (!string.IsNullOrEmpty(sheet) && CellRange.TryParseA1(range, out var compact))
            (sheet, range) = Compact(sheet, compact);

        var sources = chart.SeriesSources;
        if (chart.HasSeriesSources)
        {
            var rewritten = chart.SeriesSources!.Select(source => new ChartSeriesSource(
                Reference(source.Name), Reference(source.Values), Reference(source.Categories))).ToList();
            if (!rewritten.SequenceEqual(chart.SeriesSources!)) sources = rewritten;
        }

        return sheet == chart.SourceSheet && range == chart.SourceRange && ReferenceEquals(sources, chart.SeriesSources)
            ? chart
            : chart with { SourceSheet = sheet, SourceRange = range, SeriesSources = sources };
    }

    private string? Reference(string? text)
    {
        if (References.Read(text) is not { } reference) return text;
        var (sheet, range) = Compact(reference.Sheet, reference.Range);
        return CellRange.TryParseA1(range, out var parsed) ? References.Write(sheet, parsed) : ChartSourceReferences.Broken;
    }

    protected abstract (string Sheet, string Range) Compact(string sheet, CellRange range);

    public void Rewrite(Worksheet owner)
    {
        for (var index = 0; index < owner.Objects.Count; index++)
            if (owner.Objects[index] is { Content: ChartDefinition chart } placed && Rewrite(chart) is var rewritten && !ReferenceEquals(rewritten, chart))
                owner.Objects[index] = placed with { Content = rewritten };
    }
}

internal sealed class ChartStructureRewrite(StructureChange change) : ChartReferenceRewrite
{
    private readonly StructureShift _shift = new(change);

    protected override (string Sheet, string Range) Compact(string sheet, CellRange range)
    {
        if (!string.Equals(sheet, change.SheetName, StringComparison.CurrentCultureIgnoreCase)) return (sheet, range.ToString());
        if (Whole(range)) return (sheet, range.ToString());
        if (!change.InBand(range.Start) || !change.InBand(range.End)) return (sheet, range.ToString());
        return _shift.Move(range) is { } moved ? (sheet, moved.ToString()) : (sheet, ChartSourceReferences.Broken);
    }

    private bool Whole(CellRange range) =>
        change.Axis == StructureAxis.Rows
            ? range.Start.Row == 0 && range.End.Row >= CellAddress.MaxRows - 1
            : range.Start.Column == 0 && range.End.Column >= CellAddress.MaxColumns - 1;
}

internal sealed class ChartSheetRewrite(string oldName, string? newName) : ChartReferenceRewrite
{
    protected override (string Sheet, string Range) Compact(string sheet, CellRange range) =>
        !string.Equals(sheet, oldName, StringComparison.CurrentCultureIgnoreCase) ? (sheet, range.ToString())
        : newName is null ? (sheet, ChartSourceReferences.Broken)
        : (newName, range.ToString());
}

internal sealed class ChartLinksPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var rewrite = new ChartStructureRewrite(edit.Change);
        foreach (var sheet in edit.Engine.Workbook.Sheets) rewrite.Rewrite(sheet);
    }
}

public sealed record ChartReferenceChange(Worksheet Owner, PlacedObject Before, PlacedObject After);

public interface IChartSheetReferences
{
    IReadOnlyList<ChartReferenceChange> Renamed(Workbook workbook, string oldName, string newName);

    IReadOnlyList<ChartReferenceChange> Removed(Workbook workbook, Worksheet removed);

    void Apply(IReadOnlyList<ChartReferenceChange> changes, bool forward);
}

public sealed class ChartSheetReferences : IChartSheetReferences
{
    public IReadOnlyList<ChartReferenceChange> Renamed(Workbook workbook, string oldName, string newName) =>
        Plan(workbook, new ChartSheetRewrite(oldName, newName), null);

    public IReadOnlyList<ChartReferenceChange> Removed(Workbook workbook, Worksheet removed)
    {
        ArgumentNullException.ThrowIfNull(removed);
        return Plan(workbook, new ChartSheetRewrite(removed.Name, null), removed);
    }

    public void Apply(IReadOnlyList<ChartReferenceChange> changes, bool forward)
    {
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var (owner, before, after) in changes)
        {
            var from = forward ? before : after;
            var index = owner.Objects.IndexOf(from);
            if (index >= 0) owner.Objects[index] = forward ? after : before;
        }
    }

    private List<ChartReferenceChange> Plan(Workbook workbook, ChartSheetRewrite rewrite, Worksheet? skipped)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var changes = new List<ChartReferenceChange>();
        foreach (var owner in workbook.Sheets.Where(sheet => !ReferenceEquals(sheet, skipped)))
            foreach (var placed in owner.Objects)
                if (placed.Content is ChartDefinition chart && rewrite.Rewrite(chart) is var rewritten && !ReferenceEquals(rewritten, chart))
                    changes.Add(new ChartReferenceChange(owner, placed, placed with { Content = rewritten }));
        return changes;
    }
}

public sealed record ObjectPlacementsMemento(IReadOnlyList<(Worksheet Sheet, IReadOnlyList<PlacedObject> Objects)> Sheets);

public interface IObjectPlacementMementos
{
    ObjectPlacementsMemento Save(Workbook workbook);

    void Restore(ObjectPlacementsMemento memento);
}

public sealed class ObjectPlacementMementos : IObjectPlacementMementos
{
    public ObjectPlacementsMemento Save(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return new ObjectPlacementsMemento([.. workbook.Sheets.Select(sheet => (sheet, (IReadOnlyList<PlacedObject>)[.. sheet.Objects]))]);
    }

    public void Restore(ObjectPlacementsMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        foreach (var (sheet, objects) in memento.Sheets)
        {
            sheet.Objects.Clear();
            sheet.Objects.AddRange(objects);
        }
    }
}
