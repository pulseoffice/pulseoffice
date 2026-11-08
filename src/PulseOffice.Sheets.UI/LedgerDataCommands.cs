using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerDataCommands(
    IActiveSheetContext context,
    ISheetOutline outline,
    IDuplicateRemover duplicates,
    ISheetCellShift shifts,
    IStatusBarService status,
    IWorkbookCheckpoints checkpoints,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Sheet("ledger.print.setRange", "sheet/print", sheet =>
        {
            sheet.PrintRanges.Clear();
            sheet.PrintRanges.Add(Context.Selection);

            status.Report(texts.Format("status.print.range", Context.Selection.Start.ToA1() + ":" + Context.Selection.End.ToA1()));
        }),

        Sheet("ledger.print.addRange", "sheet/print", sheet =>
        {
            sheet.PrintRanges.Add(Context.Selection);
            status.Report(texts.Format("status.print.range", Context.Selection.Start.ToA1() + ":" + Context.Selection.End.ToA1()));
        }),

        Sheet("ledger.print.clearRange", "sheet/print", sheet =>
        {
            sheet.PrintRanges.Clear();
            status.Report(texts["status.print.rangeCleared"]);
        }),

        Sheet("ledger.print.repeatRows", "sheet/print", sheet =>
        {
            sheet.RepeatedPrintRows = Context.Selection.End.Row + 1;
            status.Report(texts.Format("status.print.repeatRows", sheet.RepeatedPrintRows));
        }),

        Sheet("ledger.outline.groupRows", "data/outline", sheet =>
        {
            outline.Group(sheet, StructureAxis.Rows, Context.Selection.Start.Row, Context.Selection.End.Row);
        }),

        Sheet("ledger.outline.groupColumns", "data/outline", sheet =>
        {
            outline.Group(
                sheet, StructureAxis.Columns, Context.Selection.Start.Column, Context.Selection.End.Column);
        }),

        Sheet("ledger.outline.ungroupRows", "data/outline", sheet =>
        {
            outline.Ungroup(sheet, StructureAxis.Rows, Context.Selection.Start.Row, Context.Selection.End.Row);
        }),

        Sheet("ledger.outline.collapse", "data/outline", sheet =>
        {
            for (var index = 0; index < sheet.RowGroups.Count; index++)
                if (sheet.RowGroups[index].Contains(Context.Selection.Start.Row))
                    outline.Collapse(sheet, StructureAxis.Rows, index);
        }),

        Sheet("ledger.outline.expand", "data/outline", sheet =>
        {
            for (var index = 0; index < sheet.RowGroups.Count; index++)
                if (sheet.RowGroups[index].Contains(Context.Selection.Start.Row))
                    outline.Expand(sheet, StructureAxis.Rows, index);
        }),

        Prototype("ledger.data.removeDuplicates") with
        {
            MenuPath = "data/tools",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                RemoveDuplicates();
                return Task.CompletedTask;
            },
        },
    ];

    private void RemoveDuplicates()
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];
        if (sheet.Protection.IsEnabled && !sheet.Protection.AllowDeleteRows)
        {
            status.Report(texts["status.protection.format"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }
        if (BoundedToUsed(sheet, Context.Selection) is not { } area)
        {
            status.Report(texts["status.duplicates.none"], NotificationSeverity.Warning);
            return;
        }

        var plan = duplicates.Plan(sheet, new DuplicateRemovalRequest(area, HasHeader: false));

        if (plan.RowsToRemove.Count == 0)
        {
            status.Report(texts["status.duplicates.none"], NotificationSeverity.Warning);
            return;
        }

        Checkpointed(checkpoints, texts["command.ledger.data.removeDuplicates.title"], () =>
        {
            foreach (var (first, count) in Blocks(plan.RowsToRemove))
                shifts.Delete(engine, sheetIndex,
                    new CellRange(new CellAddress(first, area.Start.Column), new CellAddress(first + count - 1, area.End.Column)),
                    CellShift.Down);
            return true;
        });

        status.Report(texts.Format("status.duplicates.removed", plan.RowsToRemove.Count), NotificationSeverity.Success);
        Context.RequestRefresh();
    }

    private IEnumerable<(int First, int Count)> Blocks(IEnumerable<int> rows)
    {
        var descending = rows.Distinct().OrderByDescending(row => row).ToList();
        var index = 0;
        while (index < descending.Count)
        {
            var last = descending[index];
            var first = last;
            while (index + 1 < descending.Count && descending[index + 1] == first - 1) first = descending[++index];
            index++;
            yield return (first, last - first + 1);
        }
    }

    private CommandDefinition Sheet(string id, string path, Action<Worksheet> action) =>
        Prototype(id) with
        {
            MenuPath = path,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                if (Context.Workbook is not { } workbook) return Task.CompletedTask;

                var sheet = workbook.Sheets[Context.SheetIndex];
                if (sheet.Protection.IsEnabled)
                {
                    status.Report(texts["status.protection.format"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
                    return Task.CompletedTask;
                }

                Checkpointed(checkpoints, texts[$"command.{id}.title"], () =>
                {
                    action(sheet);
                    return true;
                });
                Context.RequestRefresh();

                return Task.CompletedTask;
            },
        };
}
