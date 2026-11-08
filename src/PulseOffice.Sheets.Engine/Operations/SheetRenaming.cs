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
    public IReadOnlyList<ChartReferenceChange> Charts { get; init; } = [];
}

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
            Charts = charts.Renamed(workbook, oldName, newName),
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

        charts.Apply(plan.Charts, forward);

        foreach (var (owner, address, before, after) in plan.Formulas)
        {
            var index = workbook.IndexOf(owner);
            if (index < 0) continue;

            engine.SetFormula(new SheetCell(index, address), forward ? after : before);
        }

        engine.RecalculateAll();
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
