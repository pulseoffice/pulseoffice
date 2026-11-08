using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record RangeMove(string SourceSheet, CellRange Source, string TargetSheet, CellAddress Target)
{
    public int RowDelta => Target.Row - Source.Start.Row;

    public int ColumnDelta => Target.Column - Source.Start.Column;

    public bool Fits =>
        Target.Row + Source.RowCount <= CellAddress.MaxRows && Target.Column + Source.ColumnCount <= CellAddress.MaxColumns;

    public CellRange TargetArea => Moved(Source);

    public bool IsIdentity => Same(SourceSheet, TargetSheet) && RowDelta == 0 && ColumnDelta == 0;

    public bool Same(string? first, string? second) => string.Equals(first, second, StringComparison.CurrentCultureIgnoreCase);

    public bool Moves(string sheet, CellRange range) => Same(sheet, SourceSheet) && Inside(range, Source);

    public bool Overwrites(string sheet, CellRange range) => Same(sheet, TargetSheet) && Inside(range, TargetArea) && !Moves(sheet, range);

    public bool Inside(CellRange range, CellRange area) => area.Contains(range.Start) && area.Contains(range.End);

    public CellAddress Moved(CellAddress address) => new(address.Row + RowDelta, address.Column + ColumnDelta);

    public CellRange Moved(CellRange range) => new(Moved(range.Start), Moved(range.End));
}

internal sealed class RangeMoveMap(RangeMove move) : IReferenceMap
{
    public MappedReference? Map(string sheet, CellRange range) =>
        move.Moves(sheet, range) ? new MappedReference(move.TargetSheet, move.Moved(range))
        : move.Overwrites(sheet, range) ? new MappedReference(sheet, null)
        : null;
}

public sealed class MoveRewriter(RangeMove move, string? formulaSheet, string? hostSheet) : FormulaRewriter
{
    private readonly RangeMoveMap _map = new(move);

    public override FormulaNode VisitReference(ReferenceNode node)
    {
        if (Mapped(node.SheetName, new CellRange(node.Address, node.Address)) is not var (sheet, range)) return node;
        return range is not { } moved ? Broken() : node with { SheetName = Qualified(node.SheetName, sheet), Address = moved.Start };
    }

    public override FormulaNode VisitSpillReference(SpillReferenceNode node)
    {
        if (Mapped(node.SheetName, new CellRange(node.Anchor, node.Anchor)) is not var (sheet, range)) return node;
        return range is not { } moved ? Broken() : node with { SheetName = Qualified(node.SheetName, sheet), Anchor = moved.Start };
    }

    public override FormulaNode VisitRange(RangeNode node)
    {
        if (node.LastSheetName is not null) return node;
        if (Mapped(node.SheetName, node.Range) is not var (sheet, range)) return node;
        return range is not { } moved ? Broken() : node with { SheetName = Qualified(node.SheetName, sheet), Range = moved };
    }

    private (string Sheet, CellRange? Range)? Mapped(string? written, CellRange range)
    {
        var sheet = written ?? formulaSheet;
        if (sheet is null) return null;
        return _map.Map(sheet, range) is { } mapped ? (mapped.Sheet, mapped.Range) : Requalified(written, sheet, range);
    }

    private (string Sheet, CellRange? Range)? Requalified(string? written, string sheet, CellRange range) =>
        written is null && hostSheet is not null && !move.Same(sheet, hostSheet) ? (sheet, range) : null;

    private string? Qualified(string? written, string sheet) =>
        written is null && move.Same(sheet, hostSheet) ? null
        : written is not null && move.Same(written, sheet) ? written
        : sheet;
}

public interface ISheetRangeMove
{
    bool KeepsBlocksWhole(Workbook workbook, RangeMove move);

    void Move(CalculationEngine engine, RangeMove move);
}

public sealed class SheetRangeMove(ISheetStructure structure) : ISheetRangeMove
{
    public bool KeepsBlocksWhole(Workbook workbook, RangeMove move)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(move);

        if (!move.Fits || workbook.FindSheet(move.SourceSheet) is not { } source || workbook.FindSheet(move.TargetSheet) is not { } target)
            return false;

        var sourceIndex = workbook.IndexOf(source);
        var targetIndex = workbook.IndexOf(target);
        var targetArea = move.TargetArea;

        return !SplitBlocks(workbook, move, source, sourceIndex).Any()
            && _filters.LandFree(workbook, move, source, sourceIndex, target, targetIndex)
            && !structure.RigidBlocks(workbook, sourceIndex).Any(block => Splits(move, block, move.Source))
            && !structure.RigidBlocks(workbook, targetIndex).Any(block => Splits(move, block, targetArea))
            && !workbook.Pivots.Any(pivot => move.Same(pivot.DestinationSheet, move.SourceSheet) && move.Source.Contains(pivot.Destination));
    }

    private IEnumerable<CellRange> SplitBlocks(Workbook workbook, RangeMove move, Worksheet source, int sourceIndex)
    {
        var blocks = structure.Blocks(workbook, sourceIndex).Where(block => block.IntersectsWith(move.Source)).ToList();
        foreach (var carried in _filters.On(workbook, source, sourceIndex).Where(filter => move.Inside(filter, move.Source)))
            blocks.Remove(carried);
        return blocks;
    }

    private readonly MovableFilters _filters = new();

    private bool Splits(RangeMove move, CellRange block, CellRange area) => block.IntersectsWith(area) && !move.Inside(block, area);

    public void Move(CalculationEngine engine, RangeMove move)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(move);

        var workbook = engine.Workbook;
        var source = workbook.FindSheet(move.SourceSheet) ?? throw new ArgumentException(move.SourceSheet, nameof(move));
        var target = workbook.FindSheet(move.TargetSheet) ?? throw new ArgumentException(move.TargetSheet, nameof(move));
        if (move.IsIdentity) return;

        var automatic = engine.AutomaticCalculation;
        engine.AutomaticCalculation = false;
        try
        {
            var edit = new RangeMoveEdit(engine, move, source, workbook.IndexOf(source), target, workbook.IndexOf(target));
            foreach (var part in _parts) part.Apply(edit);
        }
        finally
        {
            engine.AutomaticCalculation = automatic;
        }

        engine.RecalculateAll();
    }

    private readonly IReadOnlyList<RangeMovePart> _parts =
    [
        new MovedChangesPart(), new MovedFiltersPart(), new MovedCellsPart(), new MovedNamesPart(), new MovedChartsPart(), new MovedFormLinksPart(), new MovedRulesPart(),
        new MovedValidationsPart(), new MovedCommentsPart(), new MovedMergesPart(), new MovedObjectsPart(),
        new MovedSparklinesPart(), new MovedPivotsPart(),
    ];
}

internal sealed record RangeMoveEdit(
    CalculationEngine Engine, RangeMove Move, Worksheet Source, int SourceIndex, Worksheet Target, int TargetIndex)
{
    public Workbook Workbook => Engine.Workbook;

    public bool CrossSheet => !ReferenceEquals(Source, Target);

    public MoveRewriter Rewriter(string? formulaSheet) => new(Move, formulaSheet, formulaSheet);

    public string? Rewrite(FormulaRewriter rewriter, string? formula)
    {
        if (formula is null) return null;
        var prefixed = formula.StartsWith('=') ? formula : "=" + formula;
        if (rewriter.Rewrite(prefixed) is not { } rewritten) return formula;
        return formula.StartsWith('=') ? rewritten : rewritten[1..];
    }
}

internal abstract class RangeMovePart
{
    public abstract void Apply(RangeMoveEdit edit);
}

internal sealed class MovedCellsPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var engine = edit.Engine;
        var movedRewriter = new MoveRewriter(move, move.SourceSheet, move.TargetSheet);
        var moved = edit.Source.NonEmptyCellsIn(move.Source)
            .Select(pair => (Address: move.Moved(pair.Key), Cell: pair.Value,
                Spilled: engine.IsSpilledInto(new SheetCell(edit.SourceIndex, pair.Key)),
                Formula: pair.Value.HasFormula ? movedRewriter.Rewrite(pair.Value.Formula!) ?? pair.Value.Formula : null))
            .ToList();
        var elsewhere = Elsewhere(edit);

        Clear(edit, edit.Source, edit.SourceIndex, move.Source);
        Clear(edit, edit.Target, edit.TargetIndex, move.TargetArea);

        foreach (var (address, cell, spilled, formula) in moved)
        {
            var at = new SheetCell(edit.TargetIndex, address);
            if (cell.IsPartOfArrayFormula && !cell.IsArrayAnchor) { }
            else if (cell.IsArrayAnchor) engine.SetArrayFormula(at, move.Moved(cell.ArrayFormulaRange!.Value), formula!);
            else if (formula is not null) engine.SetFormula(at, formula);
            else if (!cell.IsSpilled && !spilled) engine.SetValue(at, cell.Value);
            edit.Target.SetOwnStyle(address, cell.HasStyle ? cell.StyleId : null);
        }

        foreach (var (index, address, formula, array) in elsewhere)
        {
            if (array is { } area) engine.SetArrayFormula(new SheetCell(index, address), area, formula);
            else engine.SetFormula(new SheetCell(index, address), formula);
        }
    }

    private List<(int Index, CellAddress Address, string Formula, CellRange? Array)> Elsewhere(RangeMoveEdit edit)
    {
        var rewrites = new List<(int, CellAddress, string, CellRange?)>();
        foreach (var sheet in edit.Workbook.Sheets)
        {
            var rewriter = edit.Rewriter(sheet.Name);
            var index = edit.Workbook.IndexOf(sheet);
            foreach (var (address, cell) in sheet.NonEmptyCells())
            {
                if (!cell.HasFormula || cell.IsPartOfArrayFormula && !cell.IsArrayAnchor) continue;
                if (ReferenceEquals(sheet, edit.Source) && edit.Move.Source.Contains(address)) continue;
                if (ReferenceEquals(sheet, edit.Target) && edit.Move.TargetArea.Contains(address)) continue;
                if (rewriter.Rewrite(cell.Formula!) is not { } rewritten || rewritten == cell.Formula) continue;
                rewrites.Add((index, address, rewritten, cell.IsArrayAnchor ? cell.ArrayFormulaRange : null));
            }
        }
        return rewrites;
    }

    private void Clear(RangeMoveEdit edit, Worksheet sheet, int index, CellRange area)
    {
        foreach (var (address, _) in sheet.NonEmptyCellsIn(area).ToList())
        {
            if (sheet[address].IsPartOfArrayFormula)
                sheet[address] = sheet[address] with { ArrayFormulaRange = null, IsArrayAnchor = false };
            edit.Engine.SetValue(new SheetCell(index, address), CellValue.Empty);
            sheet.SetOwnStyle(address, null);
        }
    }
}

internal sealed class MovedNamesPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        foreach (var name in edit.Workbook.DefinedNames.ToList())
        {
            var expression = edit.Rewrite(edit.Rewriter(name.Scope), name.Expression)!;
            if (expression == name.Expression) continue;
            edit.Workbook.RemoveName(name.Name, name.Scope);
            edit.Workbook.DefineName(name with { Expression = expression });
        }
    }
}

internal sealed class ChartMoveRewrite(RangeMove move) : ChartReferenceRewrite
{
    private readonly RangeMoveMap _map = new(move);

    protected override (string Sheet, string Range) Compact(string sheet, CellRange range) =>
        _map.Map(sheet, range) is not { } mapped ? (sheet, range.ToString())
        : mapped.Range is { } moved ? (mapped.Sheet, moved.ToString())
        : (sheet, ChartSourceReferences.Broken);
}

internal sealed class MovedChartsPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var rewrite = new ChartMoveRewrite(edit.Move);
        foreach (var sheet in edit.Workbook.Sheets) rewrite.Rewrite(sheet);
    }
}

internal sealed class MovedFormLinksPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit) => new FormControlRelinker(new RangeMoveMap(edit.Move)).Relink(edit.Workbook);
}
