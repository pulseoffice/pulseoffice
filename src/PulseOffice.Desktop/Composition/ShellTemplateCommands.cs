using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellTemplateCommands(
    IDocumentWorkspace workspace,
    IDocumentOperationsCatalog operations,
    IDocumentOpenRouter openRouter,
    IFileDialogService files,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    ITemplateLibrary library,
    PulseOffice.UI.Controls.Galleries.IGalleryNavigator navigator,
    PulseOffice.UI.Controls.Printing.IPageCanvasFactory canvases)
{
    private string Folder => library.Folder;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.templates"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => NewFromTemplateAsync(null, ct),
        },

        .. PulseOffice.Foundation.Modules.ModuleIdentity.All.Select(identity => new CommandDefinition
        {
            Id = new CommandId($"shell.file.templates.{identity.Kind.ToString().ToLowerInvariant()}"),
            MenuPath = "file",
            Surface = CommandSurface.None,
            Execute = (_, ct) => NewFromTemplateAsync(identity.Kind, ct),
        }),

        new CommandDefinition
        {
            Id = new CommandId("shell.file.openTemplate"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => EditTemplateAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.saveAsTemplate"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => operations.For(workspace.Active) is not null,
            Execute = (_, ct) => SaveAsTemplateAsync(ct),
        },
    ];

    private IReadOnlyList<string> TemplateExtensions => library.Extensions;

    private async Task NewFromTemplateAsync(PulseOffice.Foundation.Modules.ModuleKind? only, CancellationToken ct)
    {
        Directory.CreateDirectory(Folder);

        var found = library.Entries().Where(entry => only is null || entry.Module == only).ToList();
        if (found.Count == 0)
        {
            status.Report(texts.Format("template.none", Folder), NotificationSeverity.Warning);
            return;
        }

        var cards = found.Select(entry =>
        {
            var identity = PulseOffice.Foundation.Modules.ModuleIdentity.For(entry.Module);
            return new PulseOffice.UI.Controls.Templates.TemplateCard(
                entry.Path, entry.Name, entry.Module.ToString(), identity.BrandName, identity.AccentHex, entry.Modified,
                () => library.FirstPages(entry));
        }).ToList();

        var chosen = await dialogs.ShowAsync<PulseOffice.UI.Controls.Templates.TemplateCard>(
            new PulseOffice.UI.Controls.Templates.TemplateGalleryView(cards, texts, navigator, canvases),
            new DialogOptions { TitleKey = "command.shell.file.templates.title", PreferredWidth = 900 }, ct).ConfigureAwait(true);

        if (chosen is null) return;

        if (library.ModuleFor(chosen.Key) is null || !await openRouter.OpenAsNewAsync(chosen.Key, ct).ConfigureAwait(true))
            status.Report(texts.Format("template.unreadable", Path.GetFileName(chosen.Key)), NotificationSeverity.Error);
    }

    private async Task EditTemplateAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Folder);

        var chosen = await files.OpenFileInAsync(
            texts["template.open.title"],
            [new FileFilter(texts["template.filter"], [.. TemplateExtensions.Select(extension => "*" + extension)])],
            Folder,
            ct).ConfigureAwait(true);

        if (chosen is null) return;

        await using (chosen.ConfigureAwait(true))
            await openRouter.EditAsync(chosen.Path, ct).ConfigureAwait(true);
    }

    private async Task SaveAsTemplateAsync(CancellationToken ct)
    {
        if (workspace.Active is not { } document || operations.For(document) is not { } module) return;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts.Format("template.save.explanation", Folder),
                [
                    new TextField("name", texts["template.name"], module.SuggestedName(document)),
                    .. module.OfficeTemplateExtension is { } office
                        ? new FormField[] { new ChoiceField("format", texts["template.format"], [$"OpenDocument (.{module.TemplateExtension})", $"Office (.{office})"]) }
                        : [],
                ],
                texts),
            new DialogOptions { TitleKey = "command.shell.file.saveAsTemplate.title" }, ct).ConfigureAwait(true);

        if (values is null) return;

        var name = string.Concat(((string)values["name"]).Split(Path.GetInvalidFileNameChars())).Trim().TrimStart('.');
        if (name.Length == 0) return;

        document.CommitPendingEdits();
        Directory.CreateDirectory(Folder);
        var asOffice = values.TryGetValue("format", out var format) && Convert.ToInt64(format, System.Globalization.CultureInfo.InvariantCulture) == 1;
        var path = Path.Combine(Folder, $"{name}.{(asOffice ? module.OfficeTemplateExtension : module.TemplateExtension)}");

        var written = module.WriteTo(document, path);
        status.Report(
            written ? texts.Format("template.saved", Path.GetFileName(path)) : texts["template.failed"],
            written ? NotificationSeverity.Success : NotificationSeverity.Error);
    }
}
