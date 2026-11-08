using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Links;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioObjectLinkCommands(IActiveFolioContext context, IFolioObjectLinks objectLinks, IFolioLinks places, IHyperlinkPrompt prompt) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.object.hyperlink") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Editable(editor) is not null,
            Execute = (_, ct) => EditAsync(ct),
        },
        Viewing("folio.object.hyperlink.open") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && objectLinks.Target(editor) is { } target && objectLinks.EffectiveLink(editor, target) is not null,
            Execute = (_, _) =>
            {
                if (Editor is { } editor && objectLinks.Target(editor) is { } target) objectLinks.Follow(editor, target);
                return Task.CompletedTask;
            },
        },
        Action(Editing("folio.object.hyperlink.remove") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Editable(editor) is { } target && objectLinks.Link(editor, target) is not null,
        }, editor =>
        {
            if (Editable(editor) is { } target) editor.Change(() => objectLinks.Set(editor, target, null));
        }),
    ];

    private FolioObjectLinkTarget? Editable(FolioEditor editor) =>
        objectLinks.Target(editor) is { } target && editor.Document.Objects.Find(target.ObjectId) is { } item && !editor.ObjectLocked(item) ? target : null;

    private async Task EditAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || Editable(editor) is not { } target) return;
        var current = objectLinks.Link(editor, target);
        var request = new LinkRequest("command.folio.object.hyperlink.dialog.title")
        {
            Address = current ?? string.Empty,
            ShowsText = false,
            TextEditable = false,
            Places = places.Places(editor.Document),
            CanRemove = current is not null,
        };
        if (await prompt.AskAsync(request, ct).ConfigureAwait(true) is not { } choice || Editor is not { } after || !ReferenceEquals(after, editor)) return;
        editor.Change(() => objectLinks.Set(editor, target, choice.Remove ? null : choice.Address));
    }
}
