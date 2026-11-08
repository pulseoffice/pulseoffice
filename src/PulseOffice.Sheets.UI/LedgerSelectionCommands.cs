using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerSelectionCommands(
    IActiveSheetContext context,
    IReferenceCycler cycler,
    ISheetRegions regions,
    PulseOffice.UI.Controls.Services.IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Select("ledger.select.row", "Shift+Space", sheet => new CellRange(
            new CellAddress(Context.Selection.Start.Row, 0),
            new CellAddress(Context.Selection.End.Row, CellAddress.MaxColumns - 1))),

        Select("ledger.select.column", "Ctrl+Shift+Space", sheet => new CellRange(
            new CellAddress(0, Context.Selection.Start.Column),
            new CellAddress(CellAddress.MaxRows - 1, Context.Selection.End.Column))),

        Select("ledger.select.dataArea", "Ctrl+Shift+8",
            sheet => regions.CurrentRegion(sheet, Context.Selection.Start)),

        Select("ledger.select.arrayFormula", null, sheet =>
        {
            if (sheet[Context.Selection.Start].ArrayFormulaRange is { } array) return array;
            status.Report(texts["status.select.noArrayFormula"], NotificationSeverity.Warning);
            return null;
        }),

        Prototype("ledger.formula.cycleReference") with
        {
            MenuPath = "sheet/formula",
            ContextPaths = ["cell/format"],
            DefaultGesture = "F4",
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex][Context.Selection.Start].HasFormula,
            Execute = (_, _) =>
            {
                CycleReference();
                return Task.CompletedTask;
            },
        },
    ];

    private CommandDefinition Select(string id, string? gesture, Func<Worksheet, CellRange?> range) =>
        Prototype(id) with
        {
            MenuPath = "edit/select",
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                if (Context.Workbook is { } workbook && range(workbook.Sheets[Context.SheetIndex]) is { } chosen)
                    Context.SelectRange(chosen);

                return Task.CompletedTask;
            },
        };

    private void CycleReference()
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var address = Context.Selection.Start;
        var sheet = workbook.Sheets[Context.SheetIndex];
        var cell = sheet[address];

        if (cell.Formula is not { Length: > 0 } formula) return;

        var cycled = cycler.Cycle(formula, formula.Length);
        if (cycled == formula) return;

        var previous = formula;
        var target = new SheetCell(Context.SheetIndex, address);

        engine.SetFormula(target, cycled);

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["command.ledger.formula.cycleReference.title"],
            redo: () => engine.SetFormula(target, cycled),
            undo: () => engine.SetFormula(target, previous)));

        Context.RequestRefresh();
    }
}
