using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations.UI.Scripting;
using PulseOffice.Scripting.Editor;
using PulseOffice.UI.Controls.Macros;

namespace PulseOffice.Presentations.UI;

public sealed class DeckMacroEventsSource(
    IActiveDeckContext context,
    IScriptApiProvider api,
    DeckScriptGlobals globals,
    IMacroEditorLauncher editor,
    ITextCatalog texts) : IMacroEventsContextSource
{
    public MacroEventsContext? Create(bool readOnly)
    {
        if (context.Target is not { } target) return null;

        var history = target.Actions.History;
        var request = new MacroEditorRequest(
            ModuleKind.Presentations, globals.Globals, api.Catalog, target.Deck.Macros,
            "command.deck.macro.editor.title", history, "macro.sample.deck");

        return new MacroEventsContext(
            new RecordedMacroLibrary(target.Deck.Macros, history, texts["history.macro"]),
            new MacroEditorGateway(editor, request),
            readOnly);
    }
}
