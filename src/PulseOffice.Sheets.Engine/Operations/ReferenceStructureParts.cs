using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class BareFormulaRewrite
{
    public string? Rewrite(StructureRewriter rewriter, string? formula)
    {
        if (string.IsNullOrEmpty(formula)) return formula;

        var prefixed = formula.StartsWith('=');
        var rewritten = rewriter.Rewrite(prefixed ? formula : "=" + formula);

        return rewritten is null ? formula : prefixed ? rewritten : rewritten.TrimStart('=');
    }
}

internal sealed class BreaksPart : StructurePart
{
    public override bool WholeLinesOnly => true;

    public override void Apply(StructureEdit edit)
    {
        var breaks = edit.Shift.Rows ? edit.Sheet.RowBreaks : edit.Sheet.ColumnBreaks;
        var moved = breaks.Select(position => edit.Shift.Move(position)).OfType<int>().Where(position => position > 0).ToList();

        breaks.Clear();
        breaks.UnionWith(moved);
    }
}

internal sealed class FrozenPanesPart : StructurePart
{
    public override bool WholeLinesOnly => true;

    public override void Apply(StructureEdit edit)
    {
        var frozen = edit.Sheet.Frozen;
        var count = edit.Shift.Rows ? frozen.Rows : frozen.Columns;
        var change = edit.Change;
        if (change.Index >= count) return;

        var limit = edit.Shift.Rows ? CellAddress.MaxRows - 1 : CellAddress.MaxColumns - 1;
        var adjusted = change.IsInsertion
            ? Math.Min(count + change.Count, limit)
            : count - (Math.Min(count, change.Index + change.Count) - change.Index);

        edit.Sheet.Frozen = edit.Shift.Rows ? frozen with { Rows = adjusted } : frozen with { Columns = adjusted };
    }
}

internal sealed class NamesPart : StructurePart
{
    private readonly BareFormulaRewrite _bare = new();

    public override void Apply(StructureEdit edit)
    {
        var workbook = edit.Engine.Workbook;

        foreach (var name in workbook.DefinedNames.ToList())
        {
            var rewriter = new StructureRewriter(edit.Change, name.Scope ?? string.Empty);
            if (_bare.Rewrite(rewriter, name.Expression) is { } expression && expression != name.Expression)
                workbook.DefineName(name with { Expression = expression });
        }
    }
}

internal sealed class ConditionsElsewherePart : StructurePart
{
    private readonly BareFormulaRewrite _bare = new();

    public override void Apply(StructureEdit edit)
    {
        foreach (var sheet in edit.Engine.Workbook.Sheets.Where(sheet => !ReferenceEquals(sheet, edit.Sheet)))
        {
            var rewriter = new StructureRewriter(edit.Change, sheet.Name);

            for (var index = 0; index < sheet.ConditionalRules.Count; index++)
            {
                var rule = sheet.ConditionalRules[index];
                var formula = _bare.Rewrite(rewriter, rule.Formula);
                if (formula != rule.Formula) sheet.ConditionalRules[index] = rule with { Formula = formula };
            }

            for (var index = 0; index < sheet.Validations.Count; index++)
            {
                var validation = sheet.Validations[index];
                var first = _bare.Rewrite(rewriter, validation.FirstFormula);
                var second = _bare.Rewrite(rewriter, validation.SecondFormula);
                if (first != validation.FirstFormula || second != validation.SecondFormula)
                    sheet.Validations[index] = validation with { FirstFormula = first, SecondFormula = second };
            }
        }
    }
}

internal sealed class QueryBindingsPart : StructurePart
{
    public override IEnumerable<CellRange> Blocks(Workbook workbook, int sheetIndex, Worksheet sheet) =>
        sheet.QueryBindings.All.Select(binding => binding.Range);

    public override void Apply(StructureEdit edit)
    {
        var bindings = edit.Sheet.QueryBindings;

        foreach (var binding in bindings.All.ToList())
        {
            if (edit.Shift.Move(binding.Range) is { } range && edit.Shift.Move(binding.Anchor) is { } anchor)
                bindings.AddOrUpdate(binding with { Range = range, Anchor = anchor });
            else
                bindings.Remove(binding.Id);
        }
    }
}

internal sealed class PivotsPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var pivots = edit.Engine.Workbook.Pivots;

        for (var index = 0; index < pivots.Count; index++)
        {
            var pivot = pivots[index];
            var moved = pivot;

            if (!pivot.IsExternal && pivot.SourceQueryBindingId is null && Same(pivot.SourceSheet, edit) && edit.Shift.Move(pivot.SourceRange) is { } source)
                moved = moved with { SourceRange = source, Fields = Fields(edit, pivot, source) };

            if (Same(pivot.DestinationSheet, edit) && edit.Shift.Move(pivot.Destination) is { } destination)
                moved = moved with { Destination = destination };

            if (moved != pivot) pivots[index] = moved;
        }
    }

    private bool Same(string sheet, StructureEdit edit) =>
        string.Equals(sheet, edit.Change.SheetName, StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<Pivot.PivotField> Fields(StructureEdit edit, Pivot.PivotDefinition pivot, CellRange source)
    {
        if (edit.Shift.Rows || !edit.Change.InBand(pivot.SourceRange.Start) || !edit.Change.InBand(pivot.SourceRange.End)) return pivot.Fields;

        return [.. pivot.Fields
            .Select(field => field.IsCalculated ? field
                : edit.Shift.Move(pivot.SourceRange.Start.Column + field.SourceColumnIndex) is { } column
                    ? field with { SourceColumnIndex = column - source.Start.Column }
                    : null)
            .OfType<Pivot.PivotField>()];
    }
}

internal sealed class SparklinesPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var sparklines = edit.Sheet.Sparklines;
        var groups = sparklines.Groups
            .Select(group => group with
            {
                Sparklines = [.. group.Sparklines
                    .Select(sparkline => edit.Shift.Move(sparkline.Cell) is { } cell && edit.Shift.Move(sparkline.Source) is { } source
                        ? new Sparkline(cell, source)
                        : null)
                    .OfType<Sparkline>()],
            })
            .Where(group => group.Sparklines.Count > 0)
            .ToList();

        sparklines.Clear();
        sparklines.AddRange(groups);
    }
}

internal sealed class ScenariosPart : StructurePart
{
    public override IEnumerable<CellRange> Blocks(Workbook workbook, int sheetIndex, Worksheet sheet) =>
        sheet.Scenarios.Select(scenario => scenario.Range);

    public override void Apply(StructureEdit edit)
    {
        var scenarios = edit.Sheet.Scenarios;
        var rewriter = new StructureRewriter(edit.Change, edit.Sheet.Name);

        for (var index = scenarios.Count - 1; index >= 0; index--)
        {
            var scenario = scenarios[index];

            if (edit.Shift.Move(scenario.Range) is not { } range)
            {
                scenarios.RemoveAt(index);
                continue;
            }

            scenarios[index] = scenario with
            {
                Range = range,
                Values = Moved(edit, scenario.Values, value => value),
                Formulas = Moved(edit, scenario.Formulas, formula => rewriter.Rewrite(formula) ?? formula),
            };
        }
    }

    private Dictionary<CellAddress, T> Moved<T>(StructureEdit edit, IReadOnlyDictionary<CellAddress, T> cells, Func<T, T> rewrite)
    {
        var moved = new Dictionary<CellAddress, T>();
        foreach (var (address, content) in cells)
            if (edit.Shift.Move(address) is { } target) moved[target] = rewrite(content);
        return moved;
    }
}

internal sealed class LabelsPart : StructurePart
{
    public override void Apply(StructureEdit edit)
    {
        var labels = edit.Sheet.Labels;
        var moved = labels.All
            .Select(label => edit.Shift.Move(label.Labels) is { } names && edit.Shift.Move(label.Data) is { } data
                ? label with { Labels = names, Data = data }
                : null)
            .OfType<LabelRange>()
            .ToList();

        labels.Clear();
        foreach (var label in moved) labels.Add(label);
    }
}
