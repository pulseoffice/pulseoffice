using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Scripting;
using PulseOffice.Sheets.UI.Scripting;
using PulseOffice.Scripting.Editor;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerMacroCommands(
    IActiveSheetContext context,
    IScriptApiProvider api,
    IScriptGlobals facades,
    IMacroEditorLauncher editor) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Macro("ledger.macro.editor") with { DefaultGesture = "Ctrl+Alt+M" },
    ];

    private CommandDefinition Macro(string id) =>
        Prototype(id) with
        {
            MenuPath = "tools/macros",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => editor.ShowAsync(
                new MacroEditorRequest(ModuleKind.Sheets, facades.Globals, api.Catalog,
                    Context.Workbook?.Macros, "command.ledger.macro.editor.title", Context.History),
                ct),
        };
}
