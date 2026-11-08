using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerCellShiftCommands(
    IActiveSheetContext context,
    ISheetCellShift shifts,
    IWorkbookCheckpoints checkpoints,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.UI.Icons.IIconCatalog icons) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.cells.insert", insert: true),
        Command("ledger.cells.delete", insert: false),
    ];

    private CommandDefinition Command(string id, bool insert) =>
        Prototype(id) with
        {
            MenuPath = "sheet/structure",
            ContextPaths = ["cell/cells/block"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => AskAsync(id, insert, ct),
        };

    private async Task AskAsync(string id, bool insert, CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        IReadOnlyList<PulseOffice.UI.Controls.Choices.IconChoice> choices = insert
            ?
            [
                new("right", "cells.shift.right", texts["cells.shift.right"]),
                new("down", "cells.shift.down", texts["cells.shift.down"]),
                new("row", "cells.entireRow", texts["cells.shift.rowAbove"]),
                new("column", "cells.entireColumn", texts["cells.shift.columnBefore"]),
            ]
            :
            [
                new("left", "cells.shift.left", texts["cells.shift.left"]),
                new("up", "cells.shift.up", texts["cells.shift.up"]),
                new("row", "cells.entireRow", texts["cells.shift.row"]),
                new("column", "cells.entireColumn", texts["cells.shift.column"]),
            ];

        var form = new FormPromptView(
            texts[insert ? "cells.insert.explanation" : "cells.delete.explanation"],
            [new PulseOffice.UI.Controls.Choices.IconChoiceField("shift", texts["cells.shift.label"], choices, icons, 1) { ShowLabels = true, Columns = 2 }],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form,
            new DialogOptions { TitleKey = $"command.{id}.title" },
            ct).ConfigureAwait(true);

        if (values is null) return;

        var shift = (CellShift)(int)Convert.ToInt64(
            values["shift"], System.Globalization.CultureInfo.InvariantCulture);

        var sheetIndex = Context.SheetIndex;
        var selection = Context.Selection;

        if (insert && !shifts.CanInsert(workbook.Sheets[sheetIndex], selection, shift))
        {
            status.Report(texts["status.cells.wouldSpill"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
            return;
        }

        if (!shifts.KeepsBlocksWhole(workbook, sheetIndex, selection, shift, insert))
        {
            status.Report(texts["status.cells.wouldSplit"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
            return;
        }

        Checkpointed(checkpoints, texts[$"command.{id}.title"], () =>
        {
            if (insert) shifts.Insert(engine, sheetIndex, selection, shift);
            else shifts.Delete(engine, sheetIndex, selection, shift);
            return true;
        });

        Context.RequestRefresh();
    }
}
