using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerPanelCommands(
    IActiveSheetContext context,
    ISidePanelHost panel) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.view.sidebarStyles", "view/panels", _ =>
        {
            panel.Show("styles");
            return Task.CompletedTask;
        }),
    ];

    private CommandDefinition Command(string id, string path, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };


}
