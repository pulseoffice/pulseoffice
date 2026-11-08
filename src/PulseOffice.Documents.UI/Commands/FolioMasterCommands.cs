using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Formats;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioMasterCommands(
    IActiveFolioContext context,
    ISubdocuments subdocuments,
    IFolioDocumentService files,
    IFolioDocumentFactory documents,
    IFileDialogService dialogs,
    IDocumentWorkspace workspace,
    ILinkedFiles links,
    PulseOffice.Documents.UI.Canvas.IFolioViewLayout web,
    IStatusBarService status,
    IImageLinks imageLinks,
    IFolioObjectEditing objects,
    ITextCatalog texts) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("folio.file.newMaster"),
            Module = PulseOffice.Foundation.Modules.ModuleKind.Documents,
            Surface = CommandSurface.Advanced,
            Execute = (_, _) =>
            {
                var created = documents.CreateEmpty();
                if (created.Model is TextDocument text) text.IsMaster = true;
                workspace.Open(created);
                return Task.CompletedTask;
            },
        },
        new CommandDefinition
        {
            Id = new CommandId("folio.file.newHtml"),
            Module = PulseOffice.Foundation.Modules.ModuleKind.Documents,
            Surface = CommandSurface.Advanced,
            Execute = (_, _) =>
            {
                web.Mode = PulseOffice.Documents.UI.Canvas.FolioViewMode.Web;
                workspace.Open(documents.CreateEmpty());
                return Task.CompletedTask;
            },
        },
        Editing("folio.insert.document") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => InsertDocumentAsync(ct) },
        Editing("folio.insert.subdocument") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => InsertSubdocumentAsync(ct) },
        Editing("folio.edit.links") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && HasLinks(editor),
            Execute = (_, ct) => LinksAsync(ct),
        },
        Editing("folio.tools.updateLinks") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && subdocuments.Linked(editor.Document).Count > 0,
            Execute = (_, ct) => UpdateLinksAsync(null, ct),
        },
    ];

    private const string SectionPrefix = "s:";
    private const string ImagePrefix = "i:";

    private List<PulseOffice.Documents.Objects.AnchoredObject> LinkedImages(FolioEditor editor) =>
    [
        .. editor.Document.Objects.All
            .Where(item => item.Content is PulseOffice.Documents.Objects.EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject { LinkPath.Length: > 0 } }),
    ];

    private bool HasLinks(FolioEditor editor) => subdocuments.Linked(editor.Document).Count > 0 || LinkedImages(editor).Count > 0;

    private PulseOffice.Drawing.Assets.RasterImageObject Raster(PulseOffice.Documents.Objects.AnchoredObject item) =>
        (PulseOffice.Drawing.Assets.RasterImageObject)((PulseOffice.Documents.Objects.EmbeddedContent)item.Content).Value;

    private List<ExternalLink> Links(FolioEditor editor) =>
    [
        .. subdocuments.Linked(editor.Document).Select(section => new ExternalLink(SectionPrefix + section.Id, section.Name, section.LinkedFile!,
            Resolve(section.LinkedFile!) is { } path && File.Exists(path))),
        .. imageLinks.Describe(LinkedImages(editor).Select(item => (ImagePrefix + item.Id, item.Name ?? item.Id, Raster(item)))),
    ];

    private async Task LinksAsync(CancellationToken ct)
    {
        while (Editor is { } editor)
        {
            if (await imageLinks.ChooseAsync(Links(editor), "command.folio.edit.links.title", ct).ConfigureAwait(true) is not { } request
                || Editor is not { } current) return;
            if (request.Id.StartsWith(SectionPrefix, StringComparison.Ordinal))
                await SectionLinkAsync(current, request.Id[SectionPrefix.Length..], request.Action, ct).ConfigureAwait(true);
            else if (request.Id.StartsWith(ImagePrefix, StringComparison.Ordinal))
                await ImageLinkAsync(current, request.Id[ImagePrefix.Length..], request.Action, ct).ConfigureAwait(true);
            else return;
        }
    }

    private async Task SectionLinkAsync(FolioEditor editor, string section, ExternalLinkAction action, CancellationToken ct)
    {
        switch (action)
        {
            case ExternalLinkAction.Update:
                await UpdateLinksAsync(section, ct).ConfigureAwait(true);
                break;
            case ExternalLinkAction.Break:
                editor.Change(() => subdocuments.Unlink(editor.Document, editor.History, section));
                status.Report(texts["folio.links.unlinked"], NotificationSeverity.Success);
                break;
            default:
                status.Report(texts["folio.links.sectionSource"]);
                break;
        }
    }

    private async Task ImageLinkAsync(FolioEditor editor, string id, ExternalLinkAction action, CancellationToken ct)
    {
        if (LinkedImages(editor).FirstOrDefault(item => item.Id == id) is not { } item) return;
        var image = Raster(item);
        var updated = action switch
        {
            ExternalLinkAction.Update => imageLinks.Update(image),
            ExternalLinkAction.ChangeSource => await imageLinks.ChangeSourceAsync(image, ct).ConfigureAwait(true),
            _ => imageLinks.Break(image),
        };
        if (updated is null)
        {
            status.Report(texts["links.unavailable"], NotificationSeverity.Error);
            return;
        }
        editor.SelectObject(id);
        objects.Replace(editor, content => content is PulseOffice.Drawing.Assets.RasterImageObject ? updated : content);
        if (action == ExternalLinkAction.Break) status.Report(texts["folio.links.unlinked"], NotificationSeverity.Success);
    }

    private async Task<(TextDocument Document, string Path, string Name)?> ChooseAsync(string titleKey, CancellationToken ct)
    {
        var chosen = await dialogs.OpenFileAsync(texts[titleKey], files.ReadableFilters, ct).ConfigureAwait(true);
        if (chosen is null) return null;
        await using (chosen.ConfigureAwait(true))
        {
            if (await files.ReadAsync(chosen.Path, ct).ConfigureAwait(true) is { } other) return (other, chosen.Path, chosen.Name);
            status.Report(texts.Format("folio.master.unreadable", chosen.Name), NotificationSeverity.Error);
            return null;
        }
    }

    private async Task InsertDocumentAsync(CancellationToken ct)
    {
        if (await ChooseAsync("command.folio.insert.document.title", ct).ConfigureAwait(true) is not { } chosen || Editor is not { } editor) return;
        editor.Paste(subdocuments.Whole(chosen.Document));
    }

    private async Task InsertSubdocumentAsync(CancellationToken ct)
    {
        if (await ChooseAsync("command.folio.insert.subdocument.title", ct).ConfigureAwait(true) is not { } chosen || Editor is not { } editor) return;
        var name = Path.GetFileNameWithoutExtension(chosen.Name);
        editor.Change(() => subdocuments.Insert(editor.Document, editor.History, editor.Caret.ParagraphId, chosen.Document, Link(chosen.Path), name));
        status.Report(texts.Format("folio.master.inserted", name), NotificationSeverity.Success);
    }

    private async Task UpdateLinksAsync(string? only, CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var updated = 0;
        var missing = new List<string>();
        var sources = new List<(string Section, TextDocument Source)>();
        foreach (var section in subdocuments.Linked(editor.Document).Where(section => only is null || section.Id == only))
        {
            if (Resolve(section.LinkedFile!) is { } path && File.Exists(path) && await files.ReadAsync(path, ct).ConfigureAwait(true) is { } source)
                sources.Add((section.Id, source));
            else missing.Add(section.Name);
        }
        if (sources.Count > 0)
            editor.Change(() =>
            {
                using (editor.History.BeginTransaction("update-links"))
                    foreach (var (section, source) in sources)
                    {
                        subdocuments.Refresh(editor.Document, editor.History, section, source);
                        updated++;
                    }
            });
        status.Report(missing.Count == 0
                ? texts.Format("folio.master.updated", updated)
                : texts.Format("folio.master.missing", string.Join(", ", missing)),
            missing.Count == 0 ? NotificationSeverity.Success : NotificationSeverity.Warning);
    }

    private string Link(string path) =>
        workspace.Active?.Path is { } master && Path.GetDirectoryName(master) is { } folder && !Path.GetRelativePath(folder, path).StartsWith("..", StringComparison.Ordinal)
            ? Path.GetRelativePath(folder, path)
            : links.ToHref(path);

    private string? Resolve(string href)
    {
        if (links.ToPath(href) is not { } path) return null;
        if (Path.IsPathRooted(path)) return path;
        return workspace.Active?.Path is { } master && Path.GetDirectoryName(master) is { } folder ? Path.GetFullPath(Path.Combine(folder, path)) : null;
    }
}
