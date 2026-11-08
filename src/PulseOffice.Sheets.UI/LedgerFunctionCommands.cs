using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFunctionCommands(
    IActiveSheetContext context,
    IFunctionRegistry functions,
    IDialogService dialogs,
    ITextCatalog texts,
    IFormulaLocalization localization,
    IFunctionCategoryLooks looks,
    PulseOffice.UI.Icons.IIconCatalog icons) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.formula.wizard") with
        {
            IconKey = "ledger.formula.wizard",
            MenuPath = "insert/formula",
            DefaultGesture = "Ctrl+F2",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => InsertAsync(ct),
        },
    ];

    private async Task InsertAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var typed = await dialogs.ShowAsync<string>(
            new FunctionWizardView(functions, texts, localization, looks, icons),
            new DialogOptions { TitleKey = "functions.heading", PreferredWidth = 760 },
            ct).ConfigureAwait(true);

        if (typed is not { Length: > 1 }) return;
        var formula = localization.ToCanonical(typed, texts.Language.ToCode());

        var address = Context.Selection.Start;
        var target = new SheetCell(Context.SheetIndex, address);
        var previous = workbook.Sheets[Context.SheetIndex][address];

        engine.SetFormula(target, formula);

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["functions.heading"],
            redo: () => engine.SetFormula(target, formula),
            undo: () =>
            {
                if (previous.HasFormula) engine.SetFormula(target, previous.Formula!);
                else engine.SetValue(target, previous.Value);
            }));

        Context.RequestRefresh();
    }
}
