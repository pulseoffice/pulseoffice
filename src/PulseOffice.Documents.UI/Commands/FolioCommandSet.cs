using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public abstract class FolioCommandSet(IActiveFolioContext context)
{
    protected IActiveFolioContext Context => context;

    protected FolioEditor? Editor => context.Target?.Editor;

    protected CommandDefinition Prototype(string id) => new()
    {
        Id = new CommandId(id),
        Module = ModuleKind.Documents,
        RequiredState = DocumentState.DocumentOpen,
        Execute = (_, _) => Task.CompletedTask,
    };

    protected CommandDefinition Editing(string id) =>
        Prototype(id) with
        {
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly(),
        };

    protected CommandDefinition Viewing(string id) =>
        Prototype(id) with
        {
            EnabledWhen = _ => Editor is not null,
        };

    protected CommandDefinition Action(CommandDefinition definition, Action<FolioEditor> run) =>
        definition with
        {
            Execute = (_, _) =>
            {
                if (Editor is { } editor) run(editor);
                return Task.CompletedTask;
            },
        };
}
