using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFormatDialogCommands(
    IActiveSheetContext context,
    ISheetFormattingService formatting,
    ICellFormatDialog dialog) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.format.cells") with
        {
            IconKey = "ledger.format.cells",
            MenuPath = "format/cells",
            ContextPaths = ["cell/format"],
            DefaultGesture = "Ctrl+1",
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => formatting.Accepts("ledger.format.cells"),
            Execute = (_, ct) => dialog.ShowAsync(FormatCellsTab.Numbers, ct),
        },
    ];
}
