using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerViewCommands(IActiveSheetContext context) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Toggle("ledger.view.formulas", "formulas", "Ctrl+`"),
        Toggle("ledger.view.gridLines", "gridLines", null),
        Toggle("ledger.view.valueHighlight", "values", null),
        Toggle("ledger.view.rowColumnHighlight", "rowColumnHighlight", null),
        Toggle("ledger.view.hiddenMarkers", "hiddenMarkers", null),
        Toggle("ledger.view.pageBreaks", "pageBreaks", null),
        Toggle("ledger.view.normal", "pageBreaks", null, invert: true),
        Toggle("ledger.view.snapToGrid", "snapToGrid", null),
    ];

    private CommandDefinition Toggle(string id, string option, string? gesture, bool invert = false) =>
        Prototype(id) with
        {
            IconKey = id,
            MenuPath = "view/show",
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            CheckedWhen = _ => Context.HasTarget && Context.ViewOption(option) != invert,
            Execute = (_, _) =>
            {
                if (invert)
                {
                    if (Context.ViewOption(option)) Context.ToggleViewOption(option);
                }
                else
                {
                    Context.ToggleViewOption(option);
                }

                return Task.CompletedTask;
            },
        };
}
