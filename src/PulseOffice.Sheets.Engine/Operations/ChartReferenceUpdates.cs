using PulseOffice.Charts;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal abstract class ChartReferenceRewrite
{
    private readonly ChartSourceReferences _references = new();
    private readonly CellLinkTargets _links = new();
    private readonly ChartErrorValueReferences _errorValues = new();

    protected IChartSourceReferences References => _references;

    protected virtual bool KeepsUnqualifiedLinks => false;

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

        var moved = sheet == chart.SourceSheet && range == chart.SourceRange && ReferenceEquals(sources, chart.SeriesSources)
            ? chart
            : chart with { SourceSheet = sheet, SourceRange = range, SeriesSources = sources };
        return _errorValues.Map(moved, text => Reference(text)!);
    }

    public PlacedObject Rewrite(PlacedObject placed, string ownerSheet)
    {
        var content = placed.Content switch
        {
            ChartDefinition chart => Rewrite(chart),
            ShapeGroupObject group => Rewrite(group, ownerSheet),
            _ => placed.Content,
        };
        var link = Link(placed.Hyperlink, ownerSheet);
        return ReferenceEquals(content, placed.Content) && link == placed.Hyperlink
            ? placed
            : placed with { Content = content, Hyperlink = link };
    }

    private ShapeGroupObject Rewrite(ShapeGroupObject group, string ownerSheet)
    {
        var children = group.Children.Select(child => Rewrite(child, ownerSheet)).ToList();
        return children.SequenceEqual(group.Children, ReferenceEqualityComparer.Instance) ? group : group with { Children = children };
    }

    private GroupChild Rewrite(GroupChild child, string ownerSheet)
    {
        var content = child.Content is ShapeGroupObject inner ? Rewrite(inner, ownerSheet) : child.Content;
        var link = Link(child.Hyperlink, ownerSheet);
        return ReferenceEquals(content, child.Content) && link == child.Hyperlink ? child : child with { Content = content, Hyperlink = link };
    }

    public string? Link(string? link, string ownerSheet)
    {
        if (_links.Read(link, ownerSheet) is not { } target) return link;
        if (target.SheetText is null && KeepsUnqualifiedLinks) return link;

        var (sheet, range) = Compact(target.Sheet, target.Range);
        if (sheet == target.Sheet && range == target.Range.ToString()) return link;

        return _links.Write(target, sheet, CellRange.TryParseA1(range, out var parsed) ? parsed : null);
    }

    public IReadOnlyList<(CellAddress Address, Cell Before, Cell After)> LinkedCells(Worksheet owner) =>
        [.. owner.NonEmptyCells()
            .Where(pair => pair.Value.Hyperlink is ['#', _, ..])
            .Select(pair => (pair.Key, pair.Value, Link: Link(pair.Value.Hyperlink, owner.Name)))
            .Where(entry => entry.Link != entry.Value.Hyperlink)
            .Select(entry => (entry.Key, entry.Value, entry.Value with { Hyperlink = entry.Link }))];

    private string? Reference(string? text)
    {
        var areas = References.Areas(text);
        if (areas.Count == 0) return text;
        var moved = new List<ChartSourceReference>();
        foreach (var reference in areas)
        {
            var (sheet, range) = Compact(reference.Sheet, reference.Range);
            if (CellRange.TryParseA1(range, out var parsed)) moved.Add(new ChartSourceReference(sheet, parsed));
        }
        if (moved.Count == 0) return ChartSourceReferences.Broken;
        return moved.Count == 1 ? References.Write(moved[0].Sheet, moved[0].Range) : References.Write(moved);
    }

    protected abstract (string Sheet, string Range) Compact(string sheet, CellRange range);

    public void Rewrite(Worksheet owner)
    {
        for (var index = 0; index < owner.Objects.Count; index++)
            if (Rewrite(owner.Objects[index], owner.Name) is var rewritten && !ReferenceEquals(rewritten, owner.Objects[index]))
                owner.Objects[index] = rewritten;

        foreach (var (address, _, after) in LinkedCells(owner)) owner[address] = after;
    }
}

internal sealed record CellLinkTarget(string Sheet, CellRange Range, string? SheetText, char Separator);

internal sealed class CellLinkTargets
{
    private const string Broken = "#REF!";

    public CellLinkTarget? Read(string? link, string ownerSheet)
    {
        if (link is not ['#', _, ..]) return null;
        var body = link[1..];
        var split = Separator(body);
        var rangeText = split < 0 ? body : body[(split + 1)..];
        if (!CellRange.TryParseA1(rangeText.Replace("$", string.Empty, StringComparison.Ordinal), out var range)) return null;
        if (split < 0) return new CellLinkTarget(ownerSheet, range, null, '.');

        var sheetText = body[..split];
        var sheet = sheetText is ['\'', _, .., '\''] ? sheetText[1..^1].Replace("''", "'", StringComparison.Ordinal) : sheetText;
        return sheet.Length == 0 ? null : new CellLinkTarget(sheet, range, sheetText, body[split]);
    }

    public string Write(CellLinkTarget target, string sheet, CellRange? range)
    {
        var rangeText = range?.ToString() ?? Broken;
        var sameSheet = string.Equals(sheet, target.Sheet, StringComparison.CurrentCulture);
        if (target.SheetText is null && sameSheet) return "#" + rangeText;
        var sheetText = sameSheet && target.SheetText is { } original ? original : Quoted(sheet, target.SheetText);
        return "#" + sheetText + target.Separator + rangeText;
    }

    private string Quoted(string sheet, string? original) =>
        original is ['\'', ..] || sheet.Any(character => !char.IsLetterOrDigit(character) && character != '_')
            ? "'" + sheet.Replace("'", "''", StringComparison.Ordinal) + "'"
            : sheet;

    private int Separator(string body)
    {
        var quoted = false;
        var dot = -1;
        for (var index = 0; index < body.Length; index++)
        {
            var character = body[index];
            if (character == '\'') quoted = !quoted;
            else if (!quoted && character == '!') return index;
            else if (!quoted && character == '.') dot = index;
        }
        return dot;
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
    protected override bool KeepsUnqualifiedLinks => true;

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

public abstract record SheetReferenceChange(Worksheet Owner)
{
    internal abstract void Apply(bool forward);
}

public sealed record ObjectReferenceChange(Worksheet Owner, PlacedObject Before, PlacedObject After) : SheetReferenceChange(Owner)
{
    internal override void Apply(bool forward)
    {
        var index = Owner.Objects.IndexOf(forward ? Before : After);
        if (index >= 0) Owner.Objects[index] = forward ? After : Before;
    }
}

public sealed record CellLinkChange(Worksheet Owner, CellAddress Address, string Before, string After) : SheetReferenceChange(Owner)
{
    internal override void Apply(bool forward)
    {
        var cell = Owner[Address];
        if (cell.Hyperlink == (forward ? Before : After)) Owner[Address] = cell with { Hyperlink = forward ? After : Before };
    }
}

public interface IChartSheetReferences
{
    IReadOnlyList<SheetReferenceChange> Renamed(Workbook workbook, string oldName, string newName);

    IReadOnlyList<SheetReferenceChange> Removed(Workbook workbook, Worksheet removed);

    void Apply(IReadOnlyList<SheetReferenceChange> changes, bool forward);
}

public sealed class ChartSheetReferences : IChartSheetReferences
{
    public IReadOnlyList<SheetReferenceChange> Renamed(Workbook workbook, string oldName, string newName) =>
        Plan(workbook, new ChartSheetRewrite(oldName, newName), null);

    public IReadOnlyList<SheetReferenceChange> Removed(Workbook workbook, Worksheet removed)
    {
        ArgumentNullException.ThrowIfNull(removed);
        return Plan(workbook, new ChartSheetRewrite(removed.Name, null), removed);
    }

    public void Apply(IReadOnlyList<SheetReferenceChange> changes, bool forward)
    {
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var change in changes) change.Apply(forward);
    }

    private List<SheetReferenceChange> Plan(Workbook workbook, ChartSheetRewrite rewrite, Worksheet? skipped)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var changes = new List<SheetReferenceChange>();
        foreach (var owner in workbook.Sheets.Where(sheet => !ReferenceEquals(sheet, skipped)))
        {
            foreach (var placed in owner.Objects)
                if (rewrite.Rewrite(placed, owner.Name) is var rewritten && !ReferenceEquals(rewritten, placed))
                    changes.Add(new ObjectReferenceChange(owner, placed, rewritten));

            foreach (var (address, before, after) in rewrite.LinkedCells(owner))
                changes.Add(new CellLinkChange(owner, address, before.Hyperlink!, after.Hyperlink!));
        }
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
