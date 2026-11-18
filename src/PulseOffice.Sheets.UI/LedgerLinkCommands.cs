using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Links;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerLinkCommands(
    IActiveSheetContext context,
    ILinkFollower links,
    ILedgerPlaceNavigation places) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.cell.hyperlink.open") with
        {
            MenuPath = "insert/link",
            ContextPaths = ["cell/notes"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => CurrentLink() is not null,
            Execute = (_, _) =>
            {
                if (CurrentLink() is { } link) links.Follow(link, place => places.GoTo(Context, place));
                return Task.CompletedTask;
            },
        },
    ];

    private string? CurrentLink() =>
        Context.Workbook is { } workbook && workbook.Sheets[Context.SheetIndex][Context.Selection.Start].Hyperlink is { Length: > 0 } link
            ? link
            : null;
}
