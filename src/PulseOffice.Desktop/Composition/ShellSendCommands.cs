using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Formats.Pdf;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellSendCommands(
    IDocumentWorkspace workspace,
    IDocumentOperationsCatalog operations,
    IPdfExporter exporter,
    IMailComposerProvider composers,
    IFileSharingProvider sharing,
    IShellOpener opener,
    IStatusBarService status,
    ITextCatalog texts,
    IDiagnosticTexts diagnostics,
    PulseOffice.Platform.ITemporaryFiles temporary)
{
    private enum Attachment { AsSaved, OpenDocument, Office, Pdf }

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Send("shell.file.sendMail", ct => MailAsync(Attachment.AsSaved, ct)),
        Send("shell.file.sendMailAsOdf", ct => MailAsync(Attachment.OpenDocument, ct)),
        Send("shell.file.sendMailAsOffice", ct => MailAsync(Attachment.Office, ct)),
        Send("shell.file.sendMailAsPdf", ct => MailAsync(Attachment.Pdf, ct)),
        Send("shell.file.sendBluetooth", BluetoothAsync),
        Send("shell.file.webPreview", _ =>
        {
            Preview();
            return Task.CompletedTask;
        }),
    ];

    private CommandDefinition Send(string id, Func<CancellationToken, Task> run) => new()
    {
        Id = new CommandId(id),
        MenuPath = "file/send",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        EnabledWhen = _ => operations.For(workspace.Active) is not null,
        Execute = (_, ct) => run(ct),
    };

    private (IDocumentOperations Operations, OpenDocument Document)? Active() =>
        workspace.Active is { } document && operations.For(document) is { } found ? (found, Committed(document)) : null;

    private static OpenDocument Committed(OpenDocument document)
    {
        document.CommitPendingEdits();
        return document;
    }

    private async Task MailAsync(Attachment kind, CancellationToken ct)
    {
        if (Active() is not { } active) return;

        var composer = composers.Current;

        if (!composer.IsAvailable)
        {
            status.Report(texts["mail.unavailable"], NotificationSeverity.Warning);
            return;
        }

        if (!composer.SupportsAttachments)
        {
            status.Report(texts["mail.noAttachments"], NotificationSeverity.Warning);
            return;
        }

        if (Export(active.Operations, active.Document, kind) is not { } file)
        {
            status.Report(texts["mail.exportFailed"], NotificationSeverity.Error);
            return;
        }

        var result = await composer.ComposeAsync(new MailDraft
        {
            Subject = active.Operations.SuggestedName(active.Document),
            Attachments = [file],
        }, ct).ConfigureAwait(true);

        status.Report(result.Succeeded
            ? texts.Format("mail.composed", composer.DisplayName)
            : texts.Format("mail.failed", diagnostics.Describe(result.Diagnostics[0])),
            result.Succeeded ? NotificationSeverity.Info : NotificationSeverity.Error);
    }

    private async Task BluetoothAsync(CancellationToken ct)
    {
        if (Active() is not { } active) return;

        var transfer = sharing.Current;

        if (!transfer.IsAvailable)
        {
            status.Report(texts["bluetooth.unavailable"], NotificationSeverity.Warning);
            return;
        }

        if (Export(active.Operations, active.Document, Attachment.AsSaved) is not { } file)
        {
            status.Report(texts["mail.exportFailed"], NotificationSeverity.Error);
            return;
        }

        var result = await transfer.SendAsync([file], ct).ConfigureAwait(true);

        status.Report(result.Succeeded
            ? texts.Format("bluetooth.sent", transfer.DisplayName)
            : texts.Format("bluetooth.failed", diagnostics.Describe(result.Diagnostics[0])),
            result.Succeeded ? NotificationSeverity.Info : NotificationSeverity.Error);
    }

    private string? Export(IDocumentOperations module, OpenDocument document, Attachment kind)
    {
        var extension = kind switch
        {
            Attachment.OpenDocument => module.NativeExtension,
            Attachment.Office => module.OfficeExtension,
            Attachment.Pdf => "pdf",
            _ => SavedExtension(document) ?? module.NativeExtension,
        };

        var folder = temporary.NewFolder("send-");
        var file = Path.Combine(folder, $"{module.SuggestedName(document)}.{extension}");

        if (extension == "pdf")
        {
            if (module.BuildPrintable(document) is not { } printable) return null;

            using var pdf = File.Create(file);
            var written = exporter.Write(printable, pdf, new PdfExportOptions(Title: module.SuggestedName(document)));
            return written.Succeeded ? file : null;
        }

        return module.WriteTo(document, file) ? file : null;
    }

    private static string? SavedExtension(OpenDocument document) =>
        document.Path is { Length: > 0 } path && Path.GetExtension(path) is { Length: > 1 } extension
            ? extension[1..].ToLowerInvariant()
            : null;

    private void Preview()
    {
        if (Active() is not { } active) return;

        if (active.Operations.RenderHtml(active.Document) is not { } html)
        {
            status.Report(texts["status.print.empty"], NotificationSeverity.Warning);
            return;
        }

        var file = Path.Combine(temporary.NewFolder("preview-"), "preview.html");

        try
        {
            File.WriteAllText(file, html);
        }
        catch (IOException failure)
        {
            status.Report(failure.Message, NotificationSeverity.Error);
            return;
        }

        var opened = opener.OpenFile(file);

        status.Report(opened.Succeeded
            ? texts["web.previewOpened"]
            : diagnostics.Describe(opened.Diagnostics[0]));
    }
}
