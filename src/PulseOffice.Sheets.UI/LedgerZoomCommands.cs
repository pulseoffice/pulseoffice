using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerZoomCommands(IActiveSheetContext context) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Zoom("ledger.view.zoom50", 0.5),
        Zoom("ledger.view.zoom75", 0.75),
        Zoom("ledger.view.zoom100", 1),
        Zoom("ledger.view.zoom150", 1.5),
        Zoom("ledger.view.zoom200", 2),
    ];

    private CommandDefinition Zoom(string id, double zoom) =>
        Prototype(id) with
        {
            MenuPath = "view/zoom",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                Context.SetZoom(zoom);
                return Task.CompletedTask;
            },
        };
}
