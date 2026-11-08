using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.Conditional;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerConditionCommands(
    IActiveSheetContext context,
    ISheetFormattingService formatting,
    IDialogService dialogs,
    IStatusBarService status,
    ConditionalEditorServices editors,
    IFormulaLocalization formulas,
    IConditionalRuleCompletion completion,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    ITextCatalog texts,
    ISheetFormatGate gate) : LedgerCommandSet(context), IConditionalRuleEditing
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.conditional.condition") with
        {
            IconKey = "ledger.conditional.greater",
            MenuPath = "format/conditional",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => formatting.Accepts("ledger.conditional.condition"),
            Execute = (_, ct) => AddConditionAsync(ct),
        },

        Prototype("ledger.conditional.manage") with
        {
            IconKey = "ledger.conditional.manage",
            MenuPath = "format/conditional",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = (_, ct) => ManageAsync(ct),
        },
    ];

    public async Task<ConditionalRule?> EditAsync(ConditionalRule rule, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (Context.Workbook is not { } workbook) return null;

        var language = texts.Language.ToCode();
        var formula = rule.Formula is { Length: > 0 } canonical ? formulas.ToDisplay(canonical, language).TrimStart('=') : string.Empty;
        while (true)
        {
            var view = new ConditionalRuleEditorView(rule, formula, sources.For(Dialogs.ReferenceShape.RangeList), editors, texts);
            var draft = await dialogs.ShowAsync<ConditionalRuleDraft>(
                view, new DialogOptions { TitleKey = "dialog.condition.title", PreferredWidth = 820 }, ct).ConfigureAwait(true);
            if (draft is null) return null;

            if (Complete(workbook, draft, language) is { } built) return built;
            status.Report(texts["status.condition.incomplete"], NotificationSeverity.Warning);
            rule = draft.Rule;
            formula = draft.TypedFormula;
        }
    }

    private ConditionalRule? Complete(Workbook workbook, ConditionalRuleDraft draft, string language)
    {
        if (reader.Read(workbook, draft.RangeText, Context.SheetIndex, Dialogs.ReferenceShape.RangeList) is not { Count: > 0 } references
            || references.Any(reference => reference.SheetIndex != Context.SheetIndex))
            return null;
        return completion.Complete(draft, [.. references.Select(reference => reference.Range)], CultureInfo.CurrentCulture, language);
    }

    private async Task AddConditionAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        var sheet = workbook.Sheets[Context.SheetIndex];
        if (gate.Refuses(sheet)) return;

        if (await EditAsync(editors.Catalog.Default(ConditionalRuleKind.CellValue, Context.Selection), ct).ConfigureAwait(true) is not { } built) return;

        var before = sheet.ConditionalRules.ToList();
        var after = before.Prepend(built).Select((rule, index) => rule with { Priority = index }).ToList();
        Replace(sheet, before, after, texts["command.ledger.conditional.condition.title"]);
    }

    private async Task ManageAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        var sheet = workbook.Sheets[Context.SheetIndex];
        if (gate.Refuses(sheet)) return;

        var before = sheet.ConditionalRules.ToList();
        var view = new ConditionalRulesManagerView(before, Context.Selection, this, editors, texts);
        var after = await dialogs.ShowAsync<IReadOnlyList<ConditionalRule>>(
            view, new DialogOptions { TitleKey = "command.ledger.conditional.manage.title", PreferredWidth = 680 }, ct).ConfigureAwait(true);
        if (after is null || after.SequenceEqual(before.OrderBy(rule => rule.Priority))) return;

        Replace(sheet, before, after, texts["command.ledger.conditional.manage.title"]);
    }

    private void Replace(Worksheet sheet, IReadOnlyList<ConditionalRule> before, IReadOnlyList<ConditionalRule> after, string title)
    {
        void Set(IReadOnlyList<ConditionalRule> rules)
        {
            sheet.ConditionalRules.Clear();
            sheet.ConditionalRules.AddRange(rules);
            Context.RequestRefresh();
        }

        Set(after);
        Context.History?.Push(new DelegateAction(title, redo: () => Set(after), undo: () => Set(before)));
    }
}
