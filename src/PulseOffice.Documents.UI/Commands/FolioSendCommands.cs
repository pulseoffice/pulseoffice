using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Navigation;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed record FolioSendServices(
    IDocumentOutline Outline,
    IAutoAbstract Abstracts,
    IOutlinePresentation Presentations,
    IFolioDocumentFactory Documents,
    IDocumentWorkspace Workspace,
    IFolioForms Forms,
    IStatusBarService Status,
    ITextCatalog Texts);

public sealed class FolioSendCommands(IActiveFolioContext context, FolioSendServices services) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Viewing("folio.file.outlineToPresentation") with { Surface = CommandSurface.Advanced, Execute = (_, _) => OutlineToPresentation() },
        Viewing("folio.file.abstract") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => AbstractAsync(presentation: false, ct) },
        Viewing("folio.file.abstractToPresentation") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => AbstractAsync(presentation: true, ct) },
    ];

    private Task OutlineToPresentation()
    {
        if (Editor is not { } editor) return Task.CompletedTask;
        var items = services.Outline.Headings(editor.Document).Select(heading => new OutlineItem(heading.Level, heading.Text)).ToList();
        Report(services.Presentations.Open(Title(editor), items));
        return Task.CompletedTask;
    }

    private async Task AbstractAsync(bool presentation, CancellationToken ct)
    {
        if (Editor is null) return;
        FormField[] fields =
        [
            new NumberField("levels", services.Texts["folio.abstract.levels"], 3, 1, 10) { Integer = true },
            new NumberField("paragraphs", services.Texts["folio.abstract.paragraphs"], 1, 0, 20) { Integer = true },
        ];
        var title = presentation ? "command.folio.file.abstractToPresentation.title" : "command.folio.file.abstract.title";
        if (await services.Forms.AskAsync(title, "folio.abstract.explanation", fields, ct).ConfigureAwait(true) is not { } values || Editor is not { } editor) return;
        var lines = services.Abstracts.Lines(editor.Document, (int)values.Number("levels", 3), (int)values.Number("paragraphs", 1));
        if (presentation)
        {
            Report(services.Presentations.Open(Title(editor), [.. lines.Select(line => new OutlineItem(line.Level, line.Text))]));
            return;
        }
        if (lines.Count == 0)
        {
            Report(false);
            return;
        }
        services.Workspace.Open(services.Documents.CreateFrom(services.Abstracts.Document(lines), services.Texts.Format("folio.abstract.name", Title(editor))));
    }

    private string Title(Editing.FolioEditor editor) =>
        editor.Document.Properties.Title is { Length: > 0 } title ? title : Path.GetFileNameWithoutExtension(editor.Document.FileName ?? services.Texts["folio.abstract.untitled"]);

    private void Report(bool opened)
    {
        if (!opened) services.Status.Report(services.Texts["folio.outline.empty"], NotificationSeverity.Warning);
    }
}
