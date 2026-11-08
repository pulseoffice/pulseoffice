using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioEditCommands(IActiveFolioContext context) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("folio.clipboard.cut") with
        {
            DefaultGesture = "Ctrl+X",
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Editor is { } editor && (editor.HasSelection || editor.SelectedObjectId is not null),
            Execute = (_, _) => Context.Target?.CutAsync() ?? Task.CompletedTask,
        },
        Prototype("folio.clipboard.copy") with
        {
            DefaultGesture = "Ctrl+C",
            EnabledWhen = _ => Context.Target?.Editor is { } editor && (editor.HasSelection || editor.SelectedObjectId is not null),
            Execute = (_, _) => Context.Target?.CopyAsync() ?? Task.CompletedTask,
        },
        Prototype("folio.clipboard.paste") with
        {
            DefaultGesture = "Ctrl+V",
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, _) => Context.Target?.PasteAsync() ?? Task.CompletedTask,
        },
        Prototype("folio.edit.selectAll") with
        {
            DefaultGesture = "Ctrl+A",
            Surface = CommandSurface.Menu | CommandSurface.Palette,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, _) =>
            {
                Context.Target?.Editor.SelectAll();
                return Task.CompletedTask;
            },
        },
        Toggle("folio.format.bold", "Ctrl+B", CharacterToggle.Bold),
        Toggle("folio.format.italic", "Ctrl+I", CharacterToggle.Italic),
        Toggle("folio.format.underline", "Ctrl+U", CharacterToggle.Underline),
    ];

    private CommandDefinition Toggle(string id, string gesture, CharacterToggle toggle) =>
        Prototype(id) with
        {
            DefaultGesture = gesture,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is not null,
            CheckedWhen = _ => Context.Target?.Editor.IsOn(toggle) == true,
            Execute = (_, _) =>
            {
                Context.Target?.Editor.Toggle(toggle);
                return Task.CompletedTask;
            },
        };
}
