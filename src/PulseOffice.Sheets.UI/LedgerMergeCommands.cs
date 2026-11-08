using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerMergeCommands(
    IActiveSheetContext context,
    ISheetMerge merges,
    ISheetSnapshots snapshots,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.merge.centre", sheet => merges.CanMerge(sheet, Context.Selection),
            (workbook, sheet) => merges.Merge(workbook, sheet, Context.Selection, MergeKind.MergeAndCentre)),

        Command("ledger.merge.cells", sheet => merges.CanMerge(sheet, Context.Selection),
            (workbook, sheet) => merges.Merge(workbook, sheet, Context.Selection, MergeKind.Merge)),

        Command("ledger.merge.split", sheet => merges.CanUnmerge(sheet, Context.Selection),
            (_, sheet) => merges.Unmerge(sheet, Context.Selection)),
    ];

    private CommandDefinition Command(
        string id, Func<Worksheet, bool> applies, Action<Workbook, Worksheet> change) =>
        Prototype(id) with
        {
            IconKey = id,
            MenuPath = "format/merge",
            ContextPaths = ["cell/format/merge"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && applies(workbook.Sheets[Context.SheetIndex]),
            Execute = (_, _) =>
            {
                if (Context.Workbook is not { } workbook) return Task.CompletedTask;
                if (Context.Engine is not { } engine) return Task.CompletedTask;

                var sheetIndex = Context.SheetIndex;
                var sheet = workbook.Sheets[sheetIndex];

                var before = snapshots.Capture(sheet);
                change(workbook, sheet);
                var after = snapshots.Capture(sheet);

                Context.History?.Push(new DelegateAction(
                    texts[$"command.{id}.title"],
                    redo: () => snapshots.Restore(engine, sheetIndex, after),
                    undo: () => snapshots.Restore(engine, sheetIndex, before)));

                Context.RequestRefresh();
                return Task.CompletedTask;
            },
        };
}
