using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record GoalSeekChoice(string Formula, double Target, string Variable);

public sealed record GoalSeekProbe(string? Problem, string Formula = "", string Current = "", string Variable = "", string VariableCurrent = "", string? Solution = null, string? Reached = null, bool Solvable = false);

public interface IGoalSeekProbe
{
    GoalSeekProbe Probe(GoalSeekChoice choice, bool solve);
}

public sealed class GoalSeekView : LedgerDialogView
{
    private readonly IGoalSeekProbe _probe;
    private readonly TextBlock _formula;
    private readonly TextBlock _current;
    private readonly TextBlock _variable;
    private readonly TextBlock _solution;
    private readonly PulseOffice.UI.Controls.Preview.IDebounceTimer _solve;

    public GoalSeekView(LedgerDialogKit kit, IGoalSeekProbe probe, string formula, IReferenceSource cells,
        PulseOffice.UI.Controls.Preview.IDebounceTimerFactory timers)
        : base(kit, "analysis.goalSeek.explanation")
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        ArgumentNullException.ThrowIfNull(timers);
        _solve = timers.Create(TimeSpan.FromMilliseconds(250), Solve);
        FormulaCell = new RangeFieldControl(new RangeField("formula", kit["analysis.formulaCell"], formula, cells), kit.Texts, kit.Metrics);
        Target = NumberBox(0, 140);
        VariableCell = new RangeFieldControl(new RangeField("variable", kit["analysis.variableCell"], string.Empty, cells), kit.Texts, kit.Metrics);
        _formula = new TextBlock { FontFamily = new FontFamily("Menlo, Consolas, monospace"), TextWrapping = TextWrapping.Wrap, FontSize = kit.Metrics.FontSizeSmall };
        _current = kit.Note();
        _variable = kit.Note();
        _solution = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };

        Layout.Preview = PreviewPane("goalSeek.preview", 240,
            Card(kit.Caption("goalSeek.formulaNow"), _formula, _current),
            Card(kit.Caption("goalSeek.variableNow"), _variable),
            Card(kit.Caption("goalSeek.result"), _solution));
        var section = Layout.AddSection((string?)null);
        section.Add(kit["analysis.formulaCell"], FormulaCell);
        section.Add(kit["analysis.target"], Target);
        section.Add(kit["analysis.variableCell"], VariableCell);
        Finish("goalSeek.confirm", section);

        FormulaCell.TextChanged += (_, _) => Refresh();
        VariableCell.TextChanged += (_, _) => Refresh();
        Target.TextChanged += (_, _) => Refresh();
        DetachedFromVisualTree += (_, _) => _solve.Dispose();
        Refresh();
    }

    public RangeFieldControl FormulaCell { get; }

    public TextBox Target { get; }

    public RangeFieldControl VariableCell { get; }

    public string Solution
    {
        get
        {
            if (_solve.IsPending) _solve.Flush();
            return _solution.Text ?? string.Empty;
        }
    }

    public GoalSeekChoice? Choice => ReadNumber(Target) is { } target ? new GoalSeekChoice(FormulaCell.Text, target, VariableCell.Text) : null;

    protected override object? Result() => Choice;

    private Border Card(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 2 };
        foreach (var child in children) stack.Children.Add(child);
        return new Border
        {
            Padding = new Thickness(Kit.Metrics.SpacingSmall),
            CornerRadius = new CornerRadius(Kit.Metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            Child = stack,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border).Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
    }

    private void Refresh()
    {
        var choice = Choice;
        var probe = choice is null ? new GoalSeekProbe(Kit["series.invalidNumber"]) : _probe.Probe(choice, solve: false);
        Show(probe);
        if (probe.Solvable)
        {
            _solution.Text = string.Empty;
            _solve.Kick();
        }
    }

    private void Solve()
    {
        if (Choice is { } choice) Show(_probe.Probe(choice, solve: true));
    }

    private void Show(GoalSeekProbe probe)
    {
        _formula.Text = probe.Formula;
        _current.Text = probe.Current.Length == 0 ? string.Empty : Kit.Texts.Format("goalSeek.currentValue", probe.Current);
        _variable.Text = probe.Variable.Length == 0 ? string.Empty : Kit.Texts.Format("goalSeek.variableValue", probe.Variable, probe.VariableCurrent);
        _solution.Text = probe.Solution is { } solution
            ? Kit.Texts.Format("goalSeek.found", probe.Variable, solution, probe.Reached ?? string.Empty)
            : probe.Problem is null ? Kit["status.analysis.noSolution"] : string.Empty;
        Report(probe.Problem);
    }
}

public sealed class EngineGoalSeekProbe(
    IGoalSeek goalSeek, Dialogs.ISheetReferenceReader reader, ISketchCells cells, Engine.Recalculation.CalculationEngine engine, int sheetIndex)
    : IGoalSeekProbe
{
    public GoalSeekProbe Probe(GoalSeekChoice choice, bool solve)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var workbook = engine.Workbook;
        if (Cell(choice.Formula) is not { } formula) return new GoalSeekProbe(cells.Texts["goalSeek.badFormula"]);
        var formulaCell = workbook.Sheets[formula.SheetIndex][formula.Address];
        if (!formulaCell.HasFormula) return new GoalSeekProbe(cells.Texts["goalSeek.notFormula"], Current: cells.Shown(formulaCell.Value));
        var shown = "=" + formulaCell.Formula;
        if (Cell(choice.Variable) is not { } variable)
            return new GoalSeekProbe(cells.Texts["goalSeek.badVariable"], shown, cells.Shown(formulaCell.Value));
        var variableCell = workbook.Sheets[variable.SheetIndex][variable.Address];
        if (variableCell.HasFormula)
            return new GoalSeekProbe(cells.Texts["goalSeek.variableFormula"], shown, cells.Shown(formulaCell.Value), variable.Address.ToA1(), cells.Shown(variableCell.Value));

        if (!solve)
            return new GoalSeekProbe(null, shown, cells.Shown(formulaCell.Value), variable.Address.ToA1(), cells.Shown(variableCell.Value), Solvable: true);

        var outcome = goalSeek.Solve(engine, new GoalSeekRequest(formula, variable, choice.Target));
        return new GoalSeekProbe(null, shown, cells.Shown(formulaCell.Value), variable.Address.ToA1(), cells.Shown(variableCell.Value),
            outcome.Found ? outcome.Value.ToString("G10", CultureInfo.CurrentCulture) : null,
            outcome.Found ? outcome.Reached.ToString("G10", CultureInfo.CurrentCulture) : null);
    }

    private SheetCell? Cell(string text) =>
        reader.Read(engine.Workbook, text ?? string.Empty, sheetIndex, Dialogs.ReferenceShape.Cell) is [var reference]
            ? new SheetCell(reference.SheetIndex, reference.Range.Start)
            : null;
}
