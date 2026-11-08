using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface ISheetStructure
{
    void InsertRows(CalculationEngine engine, int sheetIndex, int index, int count);

    void DeleteRows(CalculationEngine engine, int sheetIndex, int index, int count);

    void InsertColumns(CalculationEngine engine, int sheetIndex, int index, int count);

    void DeleteColumns(CalculationEngine engine, int sheetIndex, int index, int count);

    void ShiftBand(CalculationEngine engine, int sheetIndex, StructureChange change);

    bool SplitsBlocks(Workbook workbook, int sheetIndex, StructureChange change);

    IReadOnlyList<CellRange> Blocks(Workbook workbook, int sheetIndex);

    IReadOnlyList<CellRange> RigidBlocks(Workbook workbook, int sheetIndex);
}

public sealed class SheetStructure : ISheetStructure
{
    private readonly IReadOnlyList<StructurePart> _parts;
    private readonly ISheetObjectAnchoring _anchoring;

    public SheetStructure() : this(new SheetObjectAnchoring()) { }

    public SheetStructure(ISheetObjectAnchoring anchoring)
    {
        _anchoring = anchoring ?? throw new ArgumentNullException(nameof(anchoring));
        _parts = Parts(anchoring);
    }

    private IReadOnlyList<StructurePart> Parts(ISheetObjectAnchoring anchoring) =>
    [
        new LayoutPart(), new BreaksPart(), new FrozenPanesPart(), new CellsPart(), new NeighbourFormatPart(), new FormulasPart(),
        new NamesPart(), new ObjectsPart(anchoring), new FormLinksPart(), new ChartLinksPart(), new RulesPart(), new ValidationsPart(), new ConditionsElsewherePart(),
        new CommentsPart(), new PrintRangesPart(), new MergesPart(), new GroupsPart(), new FiltersPart(), new ExternalDataPart(),
        new QueryBindingsPart(), new PivotsPart(), new SparklinesPart(), new ScenariosPart(), new LabelsPart(), new TrackedChangesPart(),
    ];

    public void InsertRows(CalculationEngine engine, int sheetIndex, int index, int count) =>
        Apply(engine, sheetIndex, StructureAxis.Rows, index, count, insertion: true);

    public void DeleteRows(CalculationEngine engine, int sheetIndex, int index, int count) =>
        Apply(engine, sheetIndex, StructureAxis.Rows, index, count, insertion: false);

    public void InsertColumns(CalculationEngine engine, int sheetIndex, int index, int count) =>
        Apply(engine, sheetIndex, StructureAxis.Columns, index, count, insertion: true);

    public void DeleteColumns(CalculationEngine engine, int sheetIndex, int index, int count) =>
        Apply(engine, sheetIndex, StructureAxis.Columns, index, count, insertion: false);

    public void ShiftBand(CalculationEngine engine, int sheetIndex, StructureChange change)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(change);

        Run(engine, sheetIndex, change, _parts.Where(part => !part.WholeLinesOnly));
    }

    public bool SplitsBlocks(Workbook workbook, int sheetIndex, StructureChange change)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(change);

        var shift = new StructureShift(change);
        var sheet = workbook.Sheets[sheetIndex];
        return _parts.SelectMany(part => part.Blocks(workbook, sheetIndex, sheet)).Any(shift.Splits)
            || _parts.SelectMany(part => part.RigidBlocks(workbook, sheetIndex, sheet)).Any(range => shift.Splits(range) || shift.Cuts(range));
    }

    public IReadOnlyList<CellRange> Blocks(Workbook workbook, int sheetIndex)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return [.. _parts.SelectMany(part => part.Blocks(workbook, sheetIndex, workbook.Sheets[sheetIndex]))];
    }

    public IReadOnlyList<CellRange> RigidBlocks(Workbook workbook, int sheetIndex)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return [.. _parts.SelectMany(part => part.RigidBlocks(workbook, sheetIndex, workbook.Sheets[sheetIndex]))];
    }

    private void Apply(CalculationEngine engine, int sheetIndex, StructureAxis axis, int index, int count, bool insertion)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var change = new StructureChange(axis, index, count, insertion, engine.Workbook.Sheets[sheetIndex].Name);
        Run(engine, sheetIndex, change, _parts);
    }

    private void Run(CalculationEngine engine, int sheetIndex, StructureChange change, IEnumerable<StructurePart> parts)
    {
        var sheet = engine.Workbook.Sheets[sheetIndex];
        var captured = new Dictionary<PlacedObject, AnchorGeometry>(ReferenceEqualityComparer.Instance);
        foreach (var placed in sheet.Objects) captured.TryAdd(placed, _anchoring.Capture(sheet.Layout, placed));
        var edit = new StructureEdit(engine, sheetIndex, sheet, change, new StructureShift(change)) { Anchors = captured };

        var automatic = engine.AutomaticCalculation;
        engine.AutomaticCalculation = false;
        using var quiet = sheet.Layout.SuspendGeometryNotifications();
        try
        {
            foreach (var part in parts) part.Apply(edit);
        }
        finally
        {
            engine.AutomaticCalculation = automatic;
        }

        engine.RecalculateAll();
    }
}

internal sealed record StructureEdit(
    CalculationEngine Engine, int SheetIndex, Worksheet Sheet, StructureChange Change, StructureShift Shift)
{
    public IReadOnlyDictionary<PlacedObject, AnchorGeometry> Anchors { get; init; } =
        new Dictionary<PlacedObject, AnchorGeometry>(ReferenceEqualityComparer.Instance);
}

internal sealed class StructureShift(StructureChange change)
{
    private int Last => change.Index + change.Count - 1;

    public bool Rows => change.Axis == StructureAxis.Rows;

    public int PositionOf(CellAddress address) => Rows ? address.Row : address.Column;

    public bool Affects(CellAddress address) => PositionOf(address) >= change.Index && change.InBand(address);

    private bool OutsideBand(CellRange range) => !change.InBand(range.Start) || !change.InBand(range.End);

    private int Limit => Rows ? CellAddress.MaxRows : CellAddress.MaxColumns;

    public bool Splits(CellRange range)
    {
        if (change.BandFirst is not { } first || change.BandLast is not { } last) return false;

        var across = Rows ? (range.Start.Column, range.End.Column) : (range.Start.Row, range.End.Row);
        var along = Rows ? range.End.Row : range.End.Column;
        var crosses = across.Item2 >= first && across.Item1 <= last;
        var inside = across.Item1 >= first && across.Item2 <= last;
        return crosses && !inside && along >= change.Index;
    }

    public bool Cuts(CellRange range)
    {
        if (change.BandFirst is null || OutsideBand(range)) return false;

        var (first, last) = Rows ? (range.Start.Row, range.End.Row) : (range.Start.Column, range.End.Column);
        return change.Index > first && change.Index <= last
            || !change.IsInsertion && change.Index <= first && Last >= first && Last < last;
    }

    public int? Move(int position)
    {
        if (position < change.Index) return position;
        if (change.IsInsertion) return position + change.Count < Limit ? position + change.Count : null;
        return position <= Last ? null : position - change.Count;
    }

    public CellAddress? Move(CellAddress address) =>
        !change.InBand(address) ? address
        : Move(PositionOf(address)) is { } moved
            ? Rows ? new CellAddress(moved, address.Column) : new CellAddress(address.Row, moved)
            : null;

    public (int First, int Last)? Move(int first, int last)
    {
        if (change.IsInsertion)
        {
            var opening = first >= change.Index ? first + change.Count : first;
            return opening >= Limit ? null : (opening, Math.Min(last >= change.Index ? last + change.Count : last, Limit - 1));
        }

        if (first >= change.Index && last <= Last) return null;

        var start = first > Last ? first - change.Count : Math.Min(first, change.Index);
        var end = last > Last ? last - change.Count : change.Index - 1;

        return end < start ? null : (start, end);
    }

    public CellRange? Move(CellRange range)
    {
        if (OutsideBand(range)) return range;

        var span = Rows ? Move(range.Start.Row, range.End.Row) : Move(range.Start.Column, range.End.Column);
        if (span is not var (start, end)) return null;

        return Rows
            ? new CellRange(new CellAddress(start, range.Start.Column), new CellAddress(end, range.End.Column))
            : new CellRange(new CellAddress(range.Start.Row, start), new CellAddress(range.End.Row, end));
    }
}

internal abstract class StructurePart
{
    public virtual bool WholeLinesOnly => false;

    public virtual IEnumerable<CellRange> Blocks(Workbook workbook, int sheetIndex, Worksheet sheet) => [];

    public virtual IEnumerable<CellRange> RigidBlocks(Workbook workbook, int sheetIndex, Worksheet sheet) => [];

    public abstract void Apply(StructureEdit edit);
}

internal sealed class CellsPart : StructurePart
{
    public override IEnumerable<CellRange> RigidBlocks(Workbook workbook, int sheetIndex, Worksheet sheet) =>
        sheet.NonEmptyCells().Where(pair => pair.Value.IsArrayAnchor).Select(pair => pair.Value.ArrayFormulaRange!.Value);

    public override void Apply(StructureEdit edit)
    {
        var existing = edit.Sheet.NonEmptyCells().ToList();
        var moved = existing
            .Where(pair => edit.Shift.Affects(pair.Key))
            .Select(pair => (Original: pair.Key, Address: edit.Shift.Move(pair.Key), Cell: pair.Value))
            .Where(pair => pair.Address is not null)
            .ToList();
        var spilled = existing
            .Select(pair => pair.Key)
            .Where(address => edit.Engine.IsSpilledInto(new SheetCell(edit.SheetIndex, address)))
            .ToHashSet();

        foreach (var (address, _) in existing.Where(pair => edit.Shift.Affects(pair.Key)))
        {
            edit.Engine.SetValue(new SheetCell(edit.SheetIndex, address), CellValue.Empty);
            if (edit.Sheet[address].IsPartOfArrayFormula)
                edit.Sheet[address] = edit.Sheet[address] with { ArrayFormulaRange = null, IsArrayAnchor = false };
            edit.Sheet.SetOwnStyle(address, null);
        }

        foreach (var (original, address, cell) in moved)
        {
            var target = new SheetCell(edit.SheetIndex, address!.Value);

            if (cell.IsPartOfArrayFormula && !cell.IsArrayAnchor) { }
            else if (cell.IsArrayAnchor && edit.Shift.Move(cell.ArrayFormulaRange!.Value) is { } area)
                edit.Engine.SetArrayFormula(target, area, cell.Formula!);
            else if (cell.HasFormula) edit.Engine.SetFormula(target, cell.Formula!);
            else if (!cell.IsSpilled && !spilled.Contains(original)) edit.Engine.SetValue(target, cell.Value);

            edit.Sheet.SetOwnStyle(address.Value, cell.HasStyle ? cell.StyleId : null);
        }
    }
}

internal sealed class NeighbourFormatPart : StructurePart
{
    public override bool WholeLinesOnly => true;

    public override void Apply(StructureEdit edit)
    {
        var change = edit.Change;
        if (!change.IsInsertion || change.Index == 0) return;

        var sheet = edit.Sheet;
        var layout = sheet.Layout;
        var rows = edit.Shift.Rows;
        var source = change.Index - 1;
        var inserted = Enumerable.Range(change.Index, change.Count)
            .Where(position => position < (rows ? CellAddress.MaxRows : CellAddress.MaxColumns))
            .ToList();

        if (rows) layout.Styles.CopyRow(source, inserted);
        else layout.Styles.CopyColumn(source, inserted);

        if (rows) RowSizing(layout, source, inserted);

        var styled = sheet.NonEmptyCells()
            .Where(pair => edit.Shift.PositionOf(pair.Key) == source && pair.Value.HasStyle)
            .Select(pair => (pair.Key, pair.Value.StyleId))
            .ToList();

        foreach (var (address, styleId) in styled)
            foreach (var position in inserted)
                sheet.SetStyle(rows ? new CellAddress(position, address.Column) : new CellAddress(address.Row, position), styleId);
    }

    private void RowSizing(SheetLayout layout, int source, IReadOnlyList<int> inserted)
    {
        var height = layout.RowHeights.TryGetValue(source, out var own) && own.Value > 0 ? own : (Foundation.Units.Twip?)null;
        var optimal = height is null && layout.DefaultRowHeightManual && layout.IsRowAutomatic(source);
        foreach (var position in inserted)
        {
            if (height is { } size) layout.SetRowHeight(position, size);
            else if (optimal) layout.MakeRowOptimal(position);
        }
    }
}

internal sealed class FormulasPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var workbook = edit.Engine.Workbook;

        foreach (var sheet in workbook.Sheets)
        {
            var index = workbook.IndexOf(sheet);
            var rewriter = new StructureRewriter(edit.Change, sheet.Name);

            foreach (var (address, cell) in sheet.NonEmptyCells().ToList())
            {
                if (!cell.HasFormula) continue;

                var rewritten = rewriter.Rewrite(cell.Formula!);
                if (rewritten is null || rewritten == cell.Formula) continue;

                if (cell.IsPartOfArrayFormula && !cell.IsArrayAnchor) continue;
                if (cell.IsArrayAnchor) edit.Engine.SetArrayFormula(new SheetCell(index, address), cell.ArrayFormulaRange!.Value, rewritten);
                else edit.Engine.SetFormula(new SheetCell(index, address), rewritten);
            }
        }
    }
}

internal sealed class LayoutPart : StructurePart
{
    public override bool WholeLinesOnly => true;

    public override void Apply(StructureEdit edit)
    {
        var layout = edit.Sheet.Layout;
        var change = edit.Change;

        switch (edit.Shift.Rows, change.IsInsertion)
        {
            case (true, true): layout.InsertRows(change.Index, change.Count); break;
            case (true, false): layout.DeleteRows(change.Index, change.Count); break;
            case (false, true): layout.InsertColumns(change.Index, change.Count); break;
            default: layout.DeleteColumns(change.Index, change.Count); break;
        }
    }
}

internal sealed class TrackedChangesPart : StructurePart
{
    public override void Apply(StructureEdit edit) =>
        edit.Engine.Workbook.Changes.MoveCells(edit.SheetIndex, edit.Shift.Move);
}

internal sealed class ObjectsPart(ISheetObjectAnchoring anchoring) : StructurePart
{
    private readonly ISheetObjectAnchoring _anchoring = anchoring;

    public override void Apply(StructureEdit edit)
    {
        var objects = edit.Sheet.Objects;
        var layout = edit.Sheet.Layout;

        for (var index = objects.Count - 1; index >= 0; index--)
        {
            var placed = objects[index];
            var geometry = edit.Anchors.TryGetValue(placed, out var captured) ? captured : _anchoring.Capture(layout, placed);

            switch (placed.Anchoring)
            {
                case ObjectAnchoring.Fixed:
                    objects[index] = _anchoring.Settle(layout, placed, geometry);
                    break;
                case ObjectAnchoring.MoveAndSize when Stretched(edit, geometry) is { } moved:
                    objects[index] = _anchoring.Settle(layout, placed, moved);
                    break;
                case ObjectAnchoring.Move when edit.Shift.Move(placed.Anchor) is { } anchor:
                    objects[index] = placed with { Anchor = anchor };
                    break;
                default:
                    objects.RemoveAt(index);
                    break;
            }
        }
    }

    private AnchorGeometry? Stretched(StructureEdit edit, AnchorGeometry geometry)
    {
        var change = edit.Change;
        var shift = edit.Shift;
        var rows = shift.Rows;

        if (change.IsInsertion)
        {
            var start = shift.Move(geometry.Start.Cell);
            if (start is null) return null;
            var endPosition = shift.PositionOf(geometry.End.Cell);
            var endOffset = rows ? geometry.End.Y : geometry.End.X;
            var end = endPosition > change.Index || endPosition == change.Index && endOffset.Value > 0
                ? shift.Move(geometry.End.Cell) ?? geometry.End.Cell
                : geometry.End.Cell;
            return geometry with { Start = geometry.Start with { Cell = start.Value }, End = geometry.End with { Cell = end } };
        }

        var movedStart = shift.Move(geometry.Start.Cell);
        var movedEnd = shift.Move(geometry.End.Cell);
        if (movedStart is null && movedEnd is null) return null;

        var startPoint = movedStart is { } keptStart ? geometry.Start with { Cell = keptStart } : Collapsed(geometry.Start, change.Index, rows);
        var endPoint = movedEnd is { } keptEnd ? geometry.End with { Cell = keptEnd } : Collapsed(geometry.End, change.Index, rows);
        return geometry with { Start = startPoint, End = endPoint };
    }

    private CellPoint Collapsed(CellPoint point, int index, bool rows)
    {
        var limit = rows ? CellAddress.MaxRows - 1 : CellAddress.MaxColumns - 1;
        var position = Math.Min(index, limit);
        return rows
            ? new CellPoint(new CellAddress(position, point.Cell.Column), point.X, Twip.Zero)
            : new CellPoint(new CellAddress(point.Cell.Row, position), Twip.Zero, point.Y);
    }
}

internal sealed class FormLinksPart : StructurePart
{
    public override void Apply(StructureEdit edit) => new FormControlRelinker(edit.Change).Relink(edit.Engine.Workbook);
}

internal sealed class RulesPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var rules = edit.Sheet.ConditionalRules;

        for (var index = rules.Count - 1; index >= 0; index--)
        {
            if (Moved(edit, rules[index]) is { } moved) rules[index] = moved;
            else rules.RemoveAt(index);
        }
    }

    private Formatting.ConditionalRule? Moved(StructureEdit edit, Formatting.ConditionalRule rule)
    {
        var survivors = rule.Ranges
            .Select(range => (Before: range, After: edit.Shift.Move(range)))
            .Where(pair => pair.After is not null)
            .ToList();
        if (survivors.Count == 0) return null;

        var (before, after) = survivors[0];
        var formula = rule.Formula;
        if (formula is not null && before != rule.Range)
            formula = Like(formula, _translator.Translate(Prefixed(formula), before.Start.Row - rule.Anchor.Row, before.Start.Column - rule.Anchor.Column));
        if (formula is not null)
            formula = Like(formula, new StructureRewriter(edit.Change, edit.Sheet.Name).Rewrite(Prefixed(formula)));

        return rule with
        {
            Range = after!.Value,
            AdditionalRanges = [.. survivors.Skip(1).Select(pair => pair.After!.Value)],
            Formula = formula,
        };
    }

    private string Prefixed(string formula) => formula.StartsWith('=') ? formula : "=" + formula;

    private string Like(string original, string? rewritten) =>
        rewritten is null ? original : original.StartsWith('=') ? rewritten : rewritten.TrimStart('=');

    private readonly FormulaTranslator _translator = new();
}

internal sealed class ValidationsPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var validations = edit.Sheet.Validations;
        var rewriter = new StructureRewriter(edit.Change, edit.Sheet.Name);

        for (var index = validations.Count - 1; index >= 0; index--)
        {
            var validation = validations[index];

            if (edit.Shift.Move(validation.Range) is not { } range)
            {
                validations.RemoveAt(index);
                continue;
            }

            validations[index] = validation with
            {
                Range = range,
                FirstFormula = _bare.Rewrite(rewriter, validation.FirstFormula),
                SecondFormula = _bare.Rewrite(rewriter, validation.SecondFormula),
            };
        }
    }

    private readonly BareFormulaRewrite _bare = new();
}

internal sealed class CommentsPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var comments = edit.Sheet.Comments;
        var moved = comments
            .Select(pair => (Address: edit.Shift.Move(pair.Key), Comment: pair.Value))
            .Where(pair => pair.Address is not null)
            .ToList();

        comments.Clear();
        foreach (var (address, comment) in moved) comments[address!.Value] = comment;
    }
}

internal sealed class ExternalDataPart : StructurePart
{
    public override IEnumerable<CellRange> Blocks(Workbook workbook, int sheetIndex, Worksheet sheet) =>
        workbook.ExternalData.AreaLinks.Where(link => link.SheetIndex == sheetIndex).Select(link => link.Area);

    public override void Apply(StructureEdit edit)
    {
        var workbook = edit.Engine.Workbook;
        workbook.ExternalData.Shift(edit.SheetIndex, edit.Shift.Move, workbook.DatabaseRanges);
    }
}

internal sealed class PrintRangesPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var ranges = edit.Sheet.PrintRanges;

        for (var index = ranges.Count - 1; index >= 0; index--)
        {
            if (edit.Shift.Move(ranges[index]) is { } range) ranges[index] = range;
            else ranges.RemoveAt(index);
        }
    }
}

internal sealed class MergesPart : StructurePart
{
    public override IEnumerable<CellRange> RigidBlocks(Workbook workbook, int sheetIndex, Worksheet sheet) => sheet.Merges.All;

    public override void Apply(StructureEdit edit)
    {
        var moved = edit.Sheet.Merges.All
            .Select(edit.Shift.Move)
            .OfType<CellRange>()
            .Where(region => !region.IsSingleCell)
            .ToList();

        edit.Sheet.Merges.Clear();
        edit.Sheet.Merges.AddRange(moved);
    }
}

internal sealed class GroupsPart : StructurePart
{
    public override bool WholeLinesOnly => true;

    public override void Apply(StructureEdit edit)
    {
        var groups = edit.Shift.Rows ? edit.Sheet.RowGroups : edit.Sheet.ColumnGroups;

        for (var index = groups.Count - 1; index >= 0; index--)
        {
            if (edit.Shift.Move(groups[index].First, groups[index].Last) is var (first, last))
                groups[index] = groups[index] with { First = first, Last = last };
            else
                groups.RemoveAt(index);
        }
    }
}
