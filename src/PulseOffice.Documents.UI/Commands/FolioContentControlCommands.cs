using PulseOffice.Documents.Controls;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioContentControlCommands(IActiveFolioContext context, IFolioContentControls controls) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. Enum.GetValues<ContentControlKind>().Select(kind => Editing("folio.contentControl.insert." + Slug(kind)) with
        {
            Surface = kind == ContentControlKind.RichText ? CommandSurface.Common : CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.Locked(),
            Execute = async (_, ct) =>
            {
                if (Editor is { } editor) await controls.InsertAsync(editor, kind, ct).ConfigureAwait(true);
            },
        }),
        Editing("folio.contentControl.choose") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            EnabledWhen = _ => Editor is { } editor && controls.CanChoose(editor),
            Execute = async (_, ct) =>
            {
                if (Editor is { } editor) await controls.ChooseAsync(editor, ct).ConfigureAwait(true);
            },
        },
        Editing("folio.contentControl.properties") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Inside(editor) is not null,
            Execute = async (_, ct) =>
            {
                if (Editor is { } editor) await controls.PropertiesAsync(editor, ct).ConfigureAwait(true);
            },
        },
        Action(Editing("folio.contentControl.remove") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Inside(editor) is { Control.ControlLocked: false },
        }, editor => controls.Remove(editor)),
    ];

    private ContentControlSpan? Inside(FolioEditor editor) => editor.ControlAt(editor.Caret);

    private string Slug(ContentControlKind kind) => char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];
}
