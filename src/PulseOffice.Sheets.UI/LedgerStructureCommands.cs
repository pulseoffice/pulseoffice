using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.UI.Commands;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerStructureCommands(
    IActiveSheetContext context,
    ISheetStructure structure,
    IWorkbookCheckpoints checkpoints,
    ISheetRangeFilters rangeFilters,
    ITextCatalog texts,
    PulseOffice.UI.Controls.Services.IStatusBarService status) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Structural("ledger.sheet.insertRowsAbove", (engine, sheetIndex) =>
        {
            var selection = Context.Selection;
            structure.InsertRows(engine, sheetIndex, selection.Start.Row, selection.RowCount);
        }),

        Structural("ledger.sheet.insertRowsBelow", (engine, sheetIndex) =>
        {
            var selection = Context.Selection;
            structure.InsertRows(engine, sheetIndex, selection.End.Row + 1, selection.RowCount);
        }),

        Structural("ledger.sheet.deleteRows", (engine, sheetIndex) =>
        {
            var selection = Context.Selection;
            structure.DeleteRows(engine, sheetIndex, selection.Start.Row, selection.RowCount);
        }),

        Structural("ledger.sheet.insertColumnsBefore", (engine, sheetIndex) =>
        {
            var selection = Context.Selection;
            structure.InsertColumns(engine, sheetIndex, selection.Start.Column, selection.ColumnCount);
        }),

        Structural("ledger.sheet.insertColumnsAfter", (engine, sheetIndex) =>
        {
            var selection = Context.Selection;
            structure.InsertColumns(engine, sheetIndex, selection.End.Column + 1, selection.ColumnCount);
        }),

        Structural("ledger.sheet.deleteColumns", (engine, sheetIndex) =>
        {
            var selection = Context.Selection;
            structure.DeleteColumns(engine, sheetIndex, selection.Start.Column, selection.ColumnCount);
        }),
    ];

    private bool Allowed(SheetProtection protection, string id) =>
        !protection.IsEnabled || (id.Contains("delete", StringComparison.Ordinal)
            ? id.Contains("Rows", StringComparison.Ordinal) ? protection.AllowDeleteRows : protection.AllowDeleteColumns
            : id.Contains("Rows", StringComparison.Ordinal) ? protection.AllowInsertRows : protection.AllowInsertColumns);

    private bool Filtered(Workbook workbook, int sheetIndex) =>
        workbook.Sheets[sheetIndex] is { } sheet
        && (sheet.AutoFilter.IsActive || sheet.RangeFilter is not null
            || workbook.DatabaseRanges.All.Any(range => range.SheetIndex == sheetIndex && (range.RangeFilter is not null || range.Filters.Count > 0)));

    private CommandDefinition Structural(string id, Action<CalculationEngine, int> change) =>
        Prototype(id) with
        {
            MenuPath = "sheet/structure",
            ContextPaths = id.Contains("Rows", StringComparison.Ordinal)
                ? ["cell/cells/rows", "row/structure"]
                : ["cell/cells/columns", "column/structure"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                if (Context.Workbook is not { } workbook) return Task.CompletedTask;
                if (Context.Engine is not { } engine) return Task.CompletedTask;

                var sheetIndex = Context.SheetIndex;
                var sheet = workbook.Sheets[sheetIndex];
                if (!Allowed(sheet.Protection, id))
                {
                    status.Report(texts["status.protection.format"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
                    return Task.CompletedTask;
                }

                Checkpointed(checkpoints, texts[$"command.{id}.title"], () =>
                {
                    change(engine, sheetIndex);
                    if (Filtered(workbook, sheetIndex)) rangeFilters.Refresh(Context, sheet);
                    return true;
                });

                Context.RequestRefresh();
                return Task.CompletedTask;
            },
        };
}
