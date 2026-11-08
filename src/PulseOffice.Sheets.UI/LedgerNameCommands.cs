using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerNameCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Sheets.Engine.Operations.IDefinedNameRules rules,
    PulseOffice.Sheets.Engine.Evaluation.INameAnchoring anchoring,
    Dialogs.ISheetReferenceSources sources,
    DataDialogs.LedgerDialogKit kit,
    PulseOffice.Sheets.Engine.Operations.ISheetRangeReferences ranges) : LedgerCommandSet(context)
{
    private readonly PulseOffice.Sheets.Engine.Parsing.SheetNameQuoting _quoting = new();

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.name.define", "Ctrl+F3", ct => DefineAsync(ct)),
        Command("ledger.name.manage", null, ct => ManageAsync(ct)),
        Command("ledger.name.fromLabels", null, _ =>
        {
            FromLabels();
            return Task.CompletedTask;
        }),
    ];

    private CommandDefinition Command(string id, string? gesture, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            MenuPath = "sheet/names",
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => run(ct),
        };

    private async Task DefineAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var selection = Context.Selection;

        var form = new FormPromptView(
            texts["names.define.explanation"],
            [
                new TextField("name", texts["names.name"], rules.Suggested(sheet, selection)),
                new RangeField("reference", texts["names.expression"],
                    _quoting.Qualified(sheet.Name, Absolute(selection)), sources.ForExpression()),
            ],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "names.define.heading" }, ct).ConfigureAwait(true);

        if (values is null) return;
        if (values["name"] as string is not { Length: > 0 } entered) return;

        var name = entered.Trim();

        if (!rules.IsValid(name))
        {
            status.Report(texts.Format("names.invalid", name), NotificationSeverity.Error);
            return;
        }

        Define(workbook, new DefinedName(name, values["reference"] as string is { Length: > 0 } expression
            ? anchoring.FromCell(expression.Trim(), selection.Start)
            : _quoting.Qualified(sheet.Name, Absolute(selection))));
    }

    private async Task ManageAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var activeCell = Context.Selection.Start;
        var sheet = workbook.Sheets[Context.SheetIndex];
        var model = new DataDialogs.NameManagerModel(
            workbook.DefinedNames.Select(name => new DataDialogs.NameEntry(name.Name, anchoring.ToCell(name.Expression, activeCell), name.Scope, name.Name, name.Scope)),
            rules.IsValid);

        var edited = await dialogs.ShowAsync<DataDialogs.NameManagerModel>(
            new DataDialogs.NameManagerView(kit, model, new DataDialogs.SheetNameValuePreview(workbook, Context.SheetIndex, ranges),
                [.. workbook.Sheets.Select(item => item.Name)],
                _quoting.Qualified(sheet.Name, Absolute(Context.Selection)), sources.ForExpression()),
            new DialogOptions { TitleKey = "names.manage.heading" }, ct).ConfigureAwait(true);

        if (edited is null) return;

        List<DefinedName> before = [.. workbook.DefinedNames];
        List<DefinedName> after = [.. edited.Entries.Select(entry =>
        {
            var original = before.FirstOrDefault(name => name.Name == entry.Original && name.Scope == entry.OriginalScope);
            var expression = original is not null && anchoring.ToCell(original.Expression, activeCell) == entry.Expression
                ? original.Expression
                : anchoring.FromCell(entry.Expression, activeCell);
            return new DefinedName(entry.Name, expression, entry.Scope);
        })];

        if (before.Count == after.Count && before.All(after.Contains)) return;

        void Apply(IReadOnlyList<DefinedName> names)
        {
            foreach (var name in workbook.DefinedNames.ToList()) workbook.RemoveName(name.Name, name.Scope);
            foreach (var name in names) workbook.DefineName(name);
            Context.RequestRefresh();
        }

        Apply(after);
        Context.History?.Push(new DelegateAction(texts["names.manage.heading"], redo: () => Apply(after), undo: () => Apply(before)));
        status.Report(texts.Format("names.managed", after.Count));
    }

    private void FromLabels()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var selection = Context.Selection;

        if (selection.RowCount < 2) return;

        var created = new List<(DefinedName Name, DefinedName? Previous)>();

        for (var column = selection.Start.Column; column <= selection.End.Column; column++)
        {
            var label = sheet[new CellAddress(selection.Start.Row, column)].Value.ToString();
            if (label.Length == 0) continue;

            var name = rules.Sanitised(label);
            if (!rules.IsValid(name)) continue;

            var range = new CellRange(
                new CellAddress(selection.Start.Row + 1, column),
                new CellAddress(selection.End.Row, column));

            var defined = new DefinedName(name, _quoting.Qualified(sheet.Name, Absolute(range)));
            created.Add((defined, workbook.FindName(name)));
        }

        if (created.Count == 0)
        {
            status.Report(texts.Format("names.created", 0), NotificationSeverity.Success);
            return;
        }

        void Redo()
        {
            foreach (var (name, _) in created) workbook.DefineName(name);
        }

        void Undo()
        {
            foreach (var (name, previous) in Enumerable.Reverse(created))
            {
                if (previous is null) workbook.RemoveName(name.Name);
                else workbook.DefineName(previous);
            }
        }

        Redo();

        Context.History?.Push(new DelegateAction(texts["command.ledger.name.fromLabels.title"], redo: Redo, undo: Undo));

        status.Report(texts.Format("names.created", created.Count), NotificationSeverity.Success);
        Context.RequestRefresh();
    }

    private void Define(Workbook workbook, DefinedName name)
    {
        var previous = workbook.FindName(name.Name, name.Scope);

        workbook.DefineName(name);

        Context.History?.Push(new DelegateAction(
            texts["names.define.heading"],
            redo: () => workbook.DefineName(name),
            undo: () =>
            {
                if (previous is null) workbook.RemoveName(name.Name, name.Scope);
                else workbook.DefineName(previous);
            }));

        status.Report(texts.Format("names.defined", name.Name));
        Context.RequestRefresh();
    }

    private string Absolute(CellRange range) => range.IsSingleCell
        ? range.Start.ToA1(true, true)
        : $"{range.Start.ToA1(true, true)}:{range.End.ToA1(true, true)}";
}
