using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerRedactionCommands(
    IActiveSheetContext context,
    ISheetRedaction redaction,
    ISheetSnapshots snapshots,
    PulseOffice.UI.Controls.Prompts.IRedactionPrompt prompt,
    IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.tools.redact", "tools/redact", ct => RedactAsync(ct), needsSelection: true),
        Command("ledger.tools.autoRedact", "tools/redact", ct => AutoRedactAsync(ct), needsSelection: false),
    ];

    private CommandDefinition Command(
        string id, string path, Func<CancellationToken, Task> run, bool needsSelection) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        RequiredSelection = needsSelection ? SelectionKind.Cell | SelectionKind.CellRange : SelectionKind.None,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };

    private async Task RedactAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var hits = redaction.Plan(workbook.Sheets[Context.SheetIndex], Context.Selection);
        if (hits.Count == 0)
        {
            status.Report(texts["redact.nothing"], NotificationSeverity.Warning);
            return;
        }

        if (await prompt.ConfirmAsync(texts.Format("redact.question", hits.Count), "command.ledger.tools.redact.title", ct).ConfigureAwait(true))
            Apply(hits, texts["command.ledger.tools.redact.title"]);
    }

    private async Task AutoRedactAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (await prompt.ChoosePatternsAsync("command.ledger.tools.autoRedact.title", "redact.auto.explanation", ct).ConfigureAwait(true) is not { } chosen) return;

        var hits = redaction.PlanAutomatic(workbook.Sheets[Context.SheetIndex], chosen);
        if (hits.Count == 0)
        {
            status.Report(texts["redact.nothing"], NotificationSeverity.Warning);
            return;
        }

        Apply(hits, texts["command.ledger.tools.autoRedact.title"]);
    }

    private void Apply(IReadOnlyList<RedactionHit> hits, string label)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];

        var before = snapshots.Capture(sheet);

        foreach (var hit in hits)
            engine.SetValue(new SheetCell(sheetIndex, hit.Address), CellValue.Text(hit.After));

        var after = snapshots.Capture(sheet);

        Context.History?.Push(new DelegateAction(
            label,
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));

        Context.RequestRefresh();

        status.Report(texts.Format("redact.done", hits.Count), NotificationSeverity.Success);
    }
}
