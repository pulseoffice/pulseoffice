using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Scripting.Editor;
using PulseOffice.Sheets.UI.Scripting;
using PulseOffice.UI.Controls.Macros;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerMacroEventsSource(
    IActiveSheetContext context,
    IScriptApiProvider api,
    IScriptGlobals facades,
    IMacroEditorLauncher editor,
    ITextCatalog texts) : IMacroEventsContextSource
{
    public MacroEventsContext? Create(bool readOnly)
    {
        if (context.Workbook is not { } workbook || context.History is not { } history) return null;

        var request = new MacroEditorRequest(
            ModuleKind.Sheets, facades.Globals, api.Catalog, workbook.Macros, "command.ledger.macro.editor.title", history);

        return new MacroEventsContext(
            new RecordedMacroLibrary(workbook.Macros, history, texts["history.macro"]),
            new MacroEditorGateway(editor, request),
            readOnly);
    }
}
