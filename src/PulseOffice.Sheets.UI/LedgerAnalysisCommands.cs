using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerAnalysisCommands(
    IActiveSheetContext context,
    IGoalSeek goalSeek,
    ISolver solver,
    IScenarioManager scenarios,
    IDependencyTracer tracer,
    Analysis.ISolverConstraintRows constraintRows,
    Dialogs.IScenarioPlanner planner,
    IDialogService dialogs,
    IStatusBarService status,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    ITextCatalog texts,
    DataDialogs.LedgerDialogKit kit,
    DataDialogs.ILedgerDialogProbes probes,
    ISheetEditGuard guard,
    PulseOffice.UI.Controls.Preview.IDebounceTimerFactory timers) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Analysis("ledger.analysis.goalSeek", ct => GoalSeekAsync(ct)),
        Analysis("ledger.analysis.solver", ct => SolveAsync(ct)),
        Analysis("ledger.analysis.scenarios", ct => ScenariosAsync(ct)),

        Trace("ledger.trace.precedents", (engine, cell) => tracer.Precedents(engine, cell)),
        Trace("ledger.trace.dependents", (engine, cell) => tracer.Dependents(engine, cell)),
        Trace("ledger.trace.errors", (engine, cell) => tracer.ErrorSources(engine, cell)),

        Prototype("ledger.trace.clear") with
        {
            MenuPath = "tools/trace",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex].TraceArrows.Count > 0,
            Execute = (_, _) =>
            {
                if (Context.Workbook is { } workbook)
                {
                    workbook.Sheets[Context.SheetIndex].TraceArrows.Clear();
                    Context.RequestRefresh();
                }

                return Task.CompletedTask;
            },
        },

        ClearKind("ledger.trace.clearPrecedents", TraceKind.Precedent),
        ClearKind("ledger.trace.clearDependents", TraceKind.Dependent),

        Prototype("ledger.trace.refresh") with
        {
            MenuPath = "tools/trace",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex].TraceArrows.Count > 0,
            Execute = (_, _) =>
            {
                RefreshArrows();
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.trace.autoRefresh") with
        {
            MenuPath = "tools/trace",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            CheckedWhen = _ => AutoRefresh,
            Execute = (_, _) =>
            {
                AutoRefresh = !AutoRefresh;
                if (AutoRefresh) RefreshArrows();

                status.Report(texts[AutoRefresh ? "trace.autoRefresh.on" : "trace.autoRefresh.off"]);
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.trace.fillMode") with
        {
            MenuPath = "tools/trace",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            CheckedWhen = _ => FillMode,
            Execute = (_, _) =>
            {
                FillMode = !FillMode;
                status.Report(texts[FillMode ? "trace.fillMode.on" : "trace.fillMode.off"]);
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.trace.markInvalid") with
        {
            MenuPath = "tools/trace",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                MarkInvalid();
                return Task.CompletedTask;
            },
        },
    ];

    public bool AutoRefresh { get; private set; }

    public bool FillMode { get; private set; }

    private CommandDefinition ClearKind(string id, TraceKind kind) => Prototype(id) with
    {
        MenuPath = "tools/trace",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => Context.Workbook is { } workbook
            && workbook.Sheets[Context.SheetIndex].TraceArrows.Any(arrow => arrow.Kind == kind),
        Execute = (_, _) =>
        {
            if (Context.Workbook is { } workbook)
            {
                var arrows = workbook.Sheets[Context.SheetIndex].TraceArrows;
                var kept = arrows.Where(arrow => arrow.Kind != kind).ToList();

                arrows.Clear();
                arrows.AddRange(kept);

                Context.RequestRefresh();
            }

            return Task.CompletedTask;
        },
    };

    private void RefreshArrows()
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var origins = sheet.TraceArrows.Select(arrow => (arrow.To, arrow.Kind)).Distinct().ToList();

        sheet.TraceArrows.Clear();

        foreach (var (cell, kind) in origins)
        {
            var target = new SheetCell(Context.SheetIndex, cell);

            var found = kind switch
            {
                TraceKind.Precedent => tracer.Precedents(engine, target),
                TraceKind.Dependent => tracer.Dependents(engine, target),

                _ => tracer.ErrorSources(engine, target),
            };

            foreach (var arrow in found) sheet.TraceArrows.Add(arrow);
        }

        Context.RequestRefresh();
    }

    private void MarkInvalid()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var invalid = 0;

        foreach (var (address, cell) in sheet.NonEmptyCells())
            if (cell.Value.IsError) invalid++;

        status.Report(texts.Format("trace.invalid", invalid), NotificationSeverity.Warning);
    }

    private CommandDefinition Trace(
        string id, Func<Engine.Recalculation.CalculationEngine, SheetCell, IReadOnlyList<TraceArrow>> trace) =>
        Prototype(id) with
        {
            MenuPath = "tools/trace",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                if (Context.Workbook is not { } workbook) return Task.CompletedTask;
                if (Context.Engine is not { } engine) return Task.CompletedTask;

                var sheet = workbook.Sheets[Context.SheetIndex];
                var arrows = trace(engine, new SheetCell(Context.SheetIndex, Context.Selection.Start));

                if (arrows.Count == 0)
                {
                    status.Report(texts["status.trace.none"], NotificationSeverity.Warning);
                    return Task.CompletedTask;
                }

                foreach (var arrow in arrows)
                    if (!sheet.TraceArrows.Contains(arrow))
                        sheet.TraceArrows.Add(arrow);

                Context.RequestRefresh();
                status.Report(texts.Format("status.trace.drawn", arrows.Count));

                return Task.CompletedTask;
            },
        };

    private CommandDefinition Analysis(string id, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            MenuPath = "data/analysis",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => run(ct),
        };

    private async Task GoalSeekAsync(CancellationToken ct)
    {
        if (Context.Engine is not { } engine) return;

        var choice = await dialogs.ShowAsync<DataDialogs.GoalSeekChoice>(
            new DataDialogs.GoalSeekView(kit, probes.GoalSeek(engine, Context.SheetIndex), Context.Selection.Start.ToA1(), sources.For(Dialogs.ReferenceShape.Cell), timers),
            new DialogOptions { TitleKey = "dialog.goalSeek.title" }, ct).ConfigureAwait(true);

        if (choice is null) return;

        if (Cell(choice.Formula) is not { } formula || Cell(choice.Variable) is not { } variable)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var outcome = goalSeek.Solve(engine, new GoalSeekRequest(formula, variable, choice.Target));

        if (!outcome.Found)
        {
            status.Report(texts["status.analysis.noSolution"], NotificationSeverity.Warning);
            return;
        }

        if (!WriteNumbers(engine, [(variable, outcome.Value)], texts["command.ledger.analysis.goalSeek.title"])) return;
        status.Report(texts.Format("status.analysis.goalSeek", outcome.Value), NotificationSeverity.Success);
    }

    private async Task SolveAsync(CancellationToken ct)
    {
        if (Context.Engine is not { } engine) return;

        var answers = await AskAsync("solver",
        [
            new RangeField("objective", texts["analysis.objectiveCell"], Context.Selection.Start.ToA1(), sources.For(Dialogs.ReferenceShape.Cell)),
            new ChoiceField("goal", texts["analysis.goal"],
                [texts["analysis.goal.maximum"], texts["analysis.goal.minimum"], texts["analysis.goal.value"]]),
            new NumberField("target", texts["analysis.target"]),
            new RangeField("variables", texts["analysis.variableCells"], string.Empty, sources.For(Dialogs.ReferenceShape.RangeList)) { Placeholder = "A1:A3" },
            new Analysis.SolverConstraintsField("constraints", texts["analysis.constraints"], Context.Selection.Start.ToA1()),
        ], ct).ConfigureAwait(true);

        if (answers is null) return;

        if (Reference(answers, "objective") is not { } objective)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var variables = References(answers, "variables");

        if (variables.Count == 0)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var constraints = constraintRows.Build(
            answers.GetValueOrDefault("constraints") as IReadOnlyList<Analysis.SolverConstraintRow> ?? [], Context.SheetIndex);

        if (!constraints.Valid)
        {
            status.Report(texts.Format("status.analysis.badConstraint", constraints.Invalid[0] + 1), NotificationSeverity.Error);
            return;
        }

        var request = new SolverRequest
        {
            Objective = objective,
            Variables = variables,
            Goal = (int)answers["goal"] switch
            {
                1 => SolverGoal.Minimum,
                2 => SolverGoal.Value,
                _ => SolverGoal.Maximum,
            },
            TargetValue = (double)answers["target"],
            Constraints = constraints.Constraints,
        };

        var outcome = solver.Solve(engine, request);

        if (!outcome.Succeeded)
        {
            status.Report(texts["status.analysis.noSolution"], NotificationSeverity.Warning);
            return;
        }

        if (!WriteNumbers(
            engine,
            [.. variables.Select((cell, index) => (cell, outcome.Variables[index]))],
            texts["command.ledger.analysis.solver.title"])) return;

        status.Report(texts.Format("status.analysis.solver", outcome.Objective), NotificationSeverity.Success);
    }

    private async Task ScenariosAsync(CancellationToken ct)
    {
        if (Context.Engine is not { } engine || Context.Workbook is not { } workbook) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];
        var initial = sheet.Scenarios
            .Select(scenario => new Dialogs.ScenarioRow(scenario, scenario.Name, scenario.Comment ?? string.Empty, scenario.Range))
            .ToList();

        var choice = await dialogs.ShowAsync<DataDialogs.ScenarioChoice>(
            new DataDialogs.ScenarioView(kit, probes.Scenarios(workbook, sheetIndex), planner, initial, Context.Selection, sources.For(Dialogs.ReferenceShape.Range)),
            new DialogOptions { TitleKey = "dialog.scenarios.title" }, ct)
            .ConfigureAwait(true);

        if (choice?.Rows is not { } rows) return;

        if (planner.InvalidRows(rows) is [var invalid, ..])
        {
            status.Report(texts.Format("status.scenarios.badName", invalid + 1), NotificationSeverity.Error);
            return;
        }

        var outcome = planner.Build(rows,
            (range, name, comment) => scenarios.Capture(engine, sheetIndex, range, name, comment));

        List<Scenario> before = [.. sheet.Scenarios];
        List<Scenario> after = [.. outcome.Scenarios];

        void Set(List<Scenario> list)
        {
            sheet.Scenarios.Clear();
            sheet.Scenarios.AddRange(list);
        }

        var title = texts["command.ledger.analysis.scenarios.title"];
        using (Context.History?.BeginTransaction(title))
        {
            if (!before.SequenceEqual(after))
            {
                Set(after);
                Context.History?.Push(new DelegateAction(title, redo: () => Set(after), undo: () => Set(before)));
            }

            if (outcome.Apply is { } chosen)
            {
                var previous = scenarios.Apply(engine, sheetIndex, chosen);

                Context.History?.Push(new DelegateAction(
                    title,
                    redo: () =>
                    {
                        scenarios.Apply(engine, sheetIndex, chosen);
                        Context.RequestRefresh();
                    },
                    undo: () =>
                    {
                        scenarios.Restore(engine, sheetIndex, previous);
                        Context.RequestRefresh();
                    }));
            }
        }

        Context.RequestRefresh();
        status.Report(outcome.Apply is { } applied
            ? texts.Format("status.analysis.scenarioApplied", applied.Name)
            : texts.Format("status.scenarios.saved", after.Count));
    }

    private async Task<IReadOnlyDictionary<string, object>?> AskAsync(
        string key, IReadOnlyList<FormField> fields, CancellationToken ct) =>
        await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts[$"analysis.{key}.explanation"], fields, texts),
            new DialogOptions { TitleKey = $"dialog.{key}.title", PreferredWidth = 480 }, ct)
            .ConfigureAwait(true);

    private bool WriteNumbers(
        Engine.Recalculation.CalculationEngine engine,
        IReadOnlyList<(SheetCell Cell, double Value)> values, string description)
    {
        var workbook = engine.Workbook;
        if (values.Any(entry => !guard.CanWrite(workbook, workbook.Sheets[entry.Cell.SheetIndex], CellRange.Single(entry.Cell.Address))))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return false;
        }

        var previous = values
            .Select(entry => (entry.Cell, Cell: engine.Workbook.Sheets[entry.Cell.SheetIndex][entry.Cell.Address]))
            .ToList();

        void Apply()
        {
            foreach (var (cell, value) in values) engine.SetValue(cell, CellValue.Number(value));
            Context.RequestRefresh();
        }

        Apply();

        Context.History?.Push(new DelegateAction(
            description,
            redo: Apply,
            undo: () =>
            {
                foreach (var (cell, original) in previous)
                {
                    if (original.HasFormula) engine.SetFormula(cell, original.Formula!);
                    else engine.SetValue(cell, original.Value);
                }

                Context.RequestRefresh();
            }));
        return true;
    }

    private SheetCell? Cell(string text) =>
        Context.Workbook is { } workbook
        && reader.Read(workbook, text, Context.SheetIndex, Dialogs.ReferenceShape.Cell) is [var reference]
            ? new SheetCell(reference.SheetIndex, reference.Range.Start)
            : null;

    private SheetCell? Reference(IReadOnlyDictionary<string, object> answers, string key) =>
        Context.Workbook is { } workbook
        && reader.Read(workbook, (string)answers[key], Context.SheetIndex, Dialogs.ReferenceShape.Cell) is [var reference]
            ? new SheetCell(reference.SheetIndex, reference.Range.Start)
            : null;

    private const long MaximumReferencedCells = 10_000;

    private IReadOnlyList<SheetCell> References(IReadOnlyDictionary<string, object> answers, string key)
    {
        if (Context.Workbook is not { } workbook) return [];

        var text = ((string)answers[key]).Trim();
        var cells = new List<SheetCell>();

        foreach (var part in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (reader.Read(workbook, part, Context.SheetIndex, Dialogs.ReferenceShape.Range) is not [var reference]) continue;
            if (reference.Range.CellCount > MaximumReferencedCells) return [];
            cells.AddRange(reference.Range.Cells().Select(address => new SheetCell(reference.SheetIndex, address)));
        }

        return cells;
    }
}
