using PulseOffice.Documents.UI.Editing;
using PulseOffice.Formats;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioExportCommands(
    IActiveFolioContext context,
    ITextDocumentFormatRegistry formats,
    IFileDialogService files,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    IAtomicFileWriter atomic) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Exporting("folio.file.exportEpub", "epub", ask: true),
        Exporting("folio.file.exportEpubDirect", "epub", ask: false),
        Exporting("folio.file.exportXhtml", "xhtml", ask: false),
    ];

    private CommandDefinition Exporting(string id, string extension, bool ask) =>
        Viewing(id) with
        {
            MenuPath = "file/export",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => ExportAsync(extension, ask, ct),
        };

    private async Task ExportAsync(string extension, bool ask, CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var document = editor.Document;
        var properties = document.Properties;
        if (ask)
        {
            var values = await dialogs.ShowAsync<Dictionary<string, object>>(new FormPromptView(texts["folio.export.epub.explanation"],
            [
                new TextField("title", texts["pdf.export.title"], properties.Title ?? string.Empty),
                new TextField("author", texts["pdf.export.author"], properties.Author ?? string.Empty),
                new TextField("language", texts["folio.export.epub.language"], document.Language ?? string.Empty, "it-IT"),
                new TextField("description", texts["folio.export.epub.description"], properties.Description ?? string.Empty),
            ], texts), new DialogOptions { TitleKey = "command.folio.file.exportEpub.title", PreferredWidth = 460 }, ct).ConfigureAwait(true);
            if (values is null) return;
            document = document.Clone();
            document.Properties.Title = (string)values["title"];
            document.Properties.Author = (string)values["author"];
            document.Properties.Description = (string)values["description"];
            document.Language = ((string)values["language"]).Trim() is { Length: > 0 } language ? language : document.Language;
        }

        if (formats.WriterFor("export." + extension) is not { } writer) return;
        var name = (properties.Title is { Length: > 0 } title ? title : texts["document.defaultName"]) + "." + extension;
        var chosen = await files.SaveFileAsync(texts["dialog.exportEpub.title"], name, [new FileFilter(writer.Format.DisplayName, [.. writer.Format.Extensions.Select(item => item.TrimStart('.'))])], ct).ConfigureAwait(true);
        if (chosen is null) return;

        var written = await atomic.WriteAsync(chosen, stream => writer.Write(document, stream), result => result.Succeeded, ct).ConfigureAwait(true);
        status.Report(written.Succeeded ? texts.Format("folio.export.done", chosen.Name) : texts["status.export.failed"],
            written.Succeeded ? NotificationSeverity.Success : NotificationSeverity.Error);
    }
}
