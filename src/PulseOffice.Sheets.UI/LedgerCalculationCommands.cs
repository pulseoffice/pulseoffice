using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerCalculationCommands(
    IActiveSheetContext context,
    ISheetSnapshots snapshots,
    IStatusBarService status,
    ISheetEditGuard guard,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.calc.recalculate", "F9", () =>
        {
            if (Context.Engine is not { } engine) return;

            engine.RecalculateFrom(AllFormulas());
            status.Report(texts["status.calc.done"]);
        }),

        Command("ledger.calc.recalculateAll", "Ctrl+Shift+F9", () =>
        {
            if (Context.Engine is not { } engine) return;

            engine.RecalculateAll();
            status.Report(texts["status.calc.done"]);
        }),

        Command("ledger.calc.automatic", null, () =>
        {
            if (Context.Engine is not { } engine) return;

            engine.AutomaticCalculation = !engine.AutomaticCalculation;

            if (engine.AutomaticCalculation) engine.RecalculateAll();

            status.Report(texts[engine.AutomaticCalculation
                ? "status.calc.automatic"
                : "status.calc.manual"]);
        }),

        Command("ledger.calc.iterations", null, ToggleIterations),

        Command("ledger.calc.findLabels", null, ToggleFindLabels),

        Command("ledger.calc.formulaToValue", null, ConvertToValues),
    ];

    private void ToggleIterations()
    {
        var after = Toggle(settings => settings with { Iterative = !settings.Iterative }, "command.ledger.calc.iterations.title");
        if (after is not null) status.Report(texts[after.Iterative ? "status.calc.iterationsOn" : "status.calc.iterationsOff"]);
    }

    private void ToggleFindLabels()
    {
        var after = Toggle(settings => settings with { AutomaticFindLabels = !settings.AutomaticFindLabels }, "command.ledger.calc.findLabels.title");
        if (after is not null) status.Report(texts[after.AutomaticFindLabels ? "status.calc.findLabelsOn" : "status.calc.findLabelsOff"]);
    }

    private CalculationSettings? Toggle(Func<CalculationSettings, CalculationSettings> change, string titleKey)
    {
        if (Context.Workbook is not { } workbook || Context.Engine is not { } engine) return null;

        var before = workbook.Calculation;
        var after = change(before);

        void Apply(CalculationSettings settings)
        {
            workbook.Calculation = settings;
            engine.RecalculateAll();
            Context.RequestRefresh();
        }

        Apply(after);
        Context.History?.Push(new DelegateAction(
            texts[titleKey],
            redo: () => Apply(after),
            undo: () => Apply(before)));
        return after;
    }

    private IEnumerable<SheetCell> AllFormulas()
    {
        if (Context.Workbook is not { } workbook) yield break;

        for (var index = 0; index < workbook.Sheets.Count; index++)
            foreach (var (address, cell) in workbook.Sheets[index].NonEmptyCells())
                if (cell.HasFormula)
                    yield return new SheetCell(index, address);
    }

    private void ConvertToValues()
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];

        if (!guard.CanWrite(workbook, sheet, Context.Selection))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var before = snapshots.Capture(sheet);
        var converted = 0;

        foreach (var address in sheet.NonEmptyCellsIn(Context.Selection).Select(pair => pair.Key).ToList())
        {
            var cell = sheet[address];
            if (!cell.HasFormula) continue;

            var value = engine.GetValue(new SheetCell(sheetIndex, address));

            engine.SetValue(new SheetCell(sheetIndex, address), value);
            converted++;
        }

        if (converted == 0) return;

        var after = snapshots.Capture(sheet);

        Context.History?.Push(new DelegateAction(
            texts["command.ledger.calc.formulaToValue.title"],
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));

        Context.RequestRefresh();
        status.Report(texts.Format("status.calc.converted", converted), NotificationSeverity.Success);
    }

    private CommandDefinition Command(string id, string? gesture, Action run) =>
        Prototype(id) with
        {
            CheckedWhen = id switch
            {
                "ledger.calc.automatic" => _ => Context.Engine?.AutomaticCalculation == true,
                "ledger.calc.iterations" => _ => Context.Workbook?.Calculation.Iterative == true,
                "ledger.calc.findLabels" => _ => Context.Workbook?.Calculation.AutomaticFindLabels == true,
                _ => null,
            },
            MenuPath = "data/calculate",
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = id is "ledger.calc.formulaToValue" or "ledger.calc.iterations" or "ledger.calc.findLabels" ? DocumentState.ReadOnly : DocumentState.None,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                run();
                return Task.CompletedTask;
            },
        };
}
