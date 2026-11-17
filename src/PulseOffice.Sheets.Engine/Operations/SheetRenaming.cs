using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record SheetRenamePlan(
    Worksheet Sheet,
    string OldName,
    string NewName,
    IReadOnlyList<(Worksheet Sheet, CellAddress Address, string Before, string After)> Formulas,
    IReadOnlyList<(DefinedName Before, DefinedName After)> Names,
    string? PageStyleBefore,
    string? PageStyleAfter)
{
    public IReadOnlyList<SheetReferenceChange> ObjectsAndLinks { get; init; } = [];

    public IReadOnlyList<SheetRuleRename> Rules { get; init; } = [];

    public IReadOnlyList<(Pivot.PivotDefinition Before, Pivot.PivotDefinition After)> Pivots { get; init; } = [];
}

public sealed record SheetRuleRename(
    Worksheet Sheet,
    IReadOnlyList<Formatting.ConditionalRule> RulesBefore,
    IReadOnlyList<Formatting.ConditionalRule> RulesAfter,
    IReadOnlyList<DataValidation> ValidationsBefore,
    IReadOnlyList<DataValidation> ValidationsAfter);

public interface ISheetRenaming
{
    SheetRenamePlan Plan(Workbook workbook, Worksheet sheet, string newName);

    void Apply(CalculationEngine engine, SheetRenamePlan plan);

    void Revert(CalculationEngine engine, SheetRenamePlan plan);
}

public sealed class SheetRenaming(IChartSheetReferences charts) : ISheetRenaming
{
    public SheetRenaming() : this(new ChartSheetReferences())
    {
    }

    public SheetRenamePlan Plan(Workbook workbook, Worksheet sheet, string newName)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var oldName = sheet.Name;

        var formulas = workbook.Sheets
            .SelectMany(owner => owner.NonEmptyCells()
                .Where(pair => pair.Value.HasFormula)
                .Select(pair => (owner, pair.Key, Before: pair.Value.Formula!,
                    After: Rewritten(pair.Value.Formula!, oldName, newName))))
            .Where(entry => entry.After is not null)
            .Select(entry => (entry.owner, entry.Key, entry.Before, entry.After!))
            .ToList();

        var names = workbook.DefinedNames
            .Select(name => (Before: name, After: Renamed(name, oldName, newName)))
            .Where(pair => pair.After != pair.Before)
            .ToList();

        var pageStyle = string.Equals(sheet.PageStyleName, oldName, StringComparison.CurrentCultureIgnoreCase)
            && workbook.PageStyles.Find(oldName) is not null;

        return new SheetRenamePlan(sheet, oldName, newName, formulas, names,
            pageStyle ? sheet.PageStyleName : null, pageStyle ? newName : null)
        {
            ObjectsAndLinks = charts.Renamed(workbook, oldName, newName),
            Rules = [.. workbook.Sheets
                .Select(owner => RenamedRules(owner, oldName, newName))
                .OfType<SheetRuleRename>()],
            Pivots = [.. workbook.Pivots
                .Select(pivot => (Before: pivot, After: pivot with
                {
                    SourceSheet = Same(pivot.SourceSheet, oldName) ? newName : pivot.SourceSheet,
                    DestinationSheet = Same(pivot.DestinationSheet, oldName) ? newName : pivot.DestinationSheet,
                }))
                .Where(pair => pair.After != pair.Before)],
        };
    }

    public void Apply(CalculationEngine engine, SheetRenamePlan plan) => Run(engine, plan, forward: true);

    public void Revert(CalculationEngine engine, SheetRenamePlan plan) => Run(engine, plan, forward: false);

    private void Run(CalculationEngine engine, SheetRenamePlan plan, bool forward)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(plan);

        var workbook = engine.Workbook;

        plan.Sheet.Name = forward ? plan.NewName : plan.OldName;

        foreach (var (before, after) in plan.Names)
        {
            var remove = forward ? before : after;
            workbook.RemoveName(remove.Name, remove.Scope);
        }

        foreach (var (before, after) in plan.Names) workbook.DefineName(forward ? after : before);

        if (plan.PageStyleBefore is { } styleBefore && plan.PageStyleAfter is { } styleAfter)
        {
            var target = forward ? styleAfter : styleBefore;
            var source = forward ? styleBefore : styleAfter;

            if (workbook.PageStyles.Find(source) is { } style && workbook.PageStyles.Find(target) is null)
                workbook.PageStyles.Define(target, style.Setup);

            plan.Sheet.PageStyleName = target;
        }

        charts.Apply(plan.ObjectsAndLinks, forward);

        foreach (var (before, after) in plan.Pivots)
        {
            var index = workbook.Pivots.IndexOf(forward ? before : after);
            if (index >= 0) workbook.Pivots[index] = forward ? after : before;
        }

        foreach (var rename in plan.Rules)
        {
            rename.Sheet.ConditionalRules.Clear();
            rename.Sheet.ConditionalRules.AddRange(forward ? rename.RulesAfter : rename.RulesBefore);
            rename.Sheet.Validations.Clear();
            rename.Sheet.Validations.AddRange(forward ? rename.ValidationsAfter : rename.ValidationsBefore);
        }

        foreach (var (owner, address, before, after) in plan.Formulas)
        {
            var index = workbook.IndexOf(owner);
            if (index < 0) continue;

            engine.SetFormula(new SheetCell(index, address), forward ? after : before);
        }

        engine.RecalculateAll();
    }

    private bool Same(string? sheet, string name) => string.Equals(sheet, name, StringComparison.CurrentCultureIgnoreCase);

    private SheetRuleRename? RenamedRules(Worksheet owner, string oldName, string newName)
    {
        var rules = owner.ConditionalRules.Select(rule => rule.WithFormulas(text => RenamedBare(text, oldName, newName))).ToList();
        var validations = owner.Validations.Select(validation => validation with
        {
            FirstFormula = RenamedBare(validation.FirstFormula, oldName, newName),
            SecondFormula = RenamedBare(validation.SecondFormula, oldName, newName),
        }).ToList();

        return rules.SequenceEqual(owner.ConditionalRules) && validations.SequenceEqual(owner.Validations)
            ? null
            : new SheetRuleRename(owner, [.. owner.ConditionalRules], rules, [.. owner.Validations], validations);
    }

    private string? RenamedBare(string? formula, string oldName, string newName)
    {
        if (string.IsNullOrEmpty(formula)) return formula;

        var prefixed = formula.StartsWith('=');
        return Rewritten(prefixed ? formula : "=" + formula, oldName, newName) is { } rewritten
            ? prefixed ? rewritten : rewritten[1..]
            : formula;
    }

    private DefinedName Renamed(DefinedName name, string oldName, string newName)
    {
        var scope = string.Equals(name.Scope, oldName, StringComparison.CurrentCultureIgnoreCase) ? newName : name.Scope;
        var expression = name.Expression;

        var formula = expression.StartsWith('=') ? expression : "=" + expression;

        if (Rewritten(formula, oldName, newName) is { } rewritten)
            expression = expression.StartsWith('=') ? rewritten : rewritten[1..];

        return name with { Expression = expression, Scope = scope };
    }

    private string? Rewritten(string formula, string oldName, string newName)
    {
        var rewriter = new SheetNameRewriter(oldName, newName);
        var result = rewriter.Rewrite(formula);

        return rewriter.Changed ? result : null;
    }
}

public sealed class SheetNameRewriter(string oldName, string newName) : FormulaRewriter
{
    public bool Changed { get; private set; }

    private string? Renamed(string? sheetName)
    {
        if (!string.Equals(sheetName, oldName, StringComparison.CurrentCultureIgnoreCase)) return sheetName;

        Changed = true;
        return newName;
    }

    public override FormulaNode VisitReference(ReferenceNode node) => node with { SheetName = Renamed(node.SheetName) };

    public override FormulaNode VisitRange(RangeNode node) => node with { SheetName = Renamed(node.SheetName), LastSheetName = Renamed(node.LastSheetName) };

    public override FormulaNode VisitSpillReference(SpillReferenceNode node) => node with { SheetName = Renamed(node.SheetName) };
}
