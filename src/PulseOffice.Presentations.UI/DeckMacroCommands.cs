using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations.UI.Scripting;
using PulseOffice.Scripting.Editor;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public sealed class DeckMacroCommands(
    IActiveDeckContext context,
    IScriptApiProvider api,
    DeckScriptGlobals globals,
    IMacroEditorLauncher editor) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Macro("deck.macro.editor") with { DefaultGesture = "Ctrl+Alt+M" },
    ];

    private CommandDefinition Macro(string id) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => Context.Target is { } t
                ? editor.ShowAsync(
                    new MacroEditorRequest(ModuleKind.Presentations, globals.Globals, api.Catalog,
                        t.Deck.Macros, "command.deck.macro.editor.title", t.Actions.History, "macro.sample.deck"),
                    ct)
                : Task.CompletedTask,
        };
}
