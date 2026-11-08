using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Shell;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioMailMergeCommands(
    IActiveFolioContext context,
    IFolioForms forms,
    IMergeRecords records,
    IMailMergeEngine engine,
    IMergeDataReader reader,
    IFolioMergeOutput output,
    IFolioFieldRefresh refresh,
    PulseOffice.Data.UI.IDataImportFlow sources,
    PulseOffice.Document.Data.IDataTableLoader loader,
    IFileDialogService files,
    IFolioDocumentFactory documents,
    IDocumentWorkspace workspace,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Platform.IAtomicFileWriter atomic,
    IFolioMergeWizard wizard,
    PulseOffice.Data.IExternalQueryGate documentQueries) : FolioCommandSet(context)
{

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Viewing("folio.view.dataSources") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => sources.ManageConnectionsAsync(ct) },
        Editing("folio.mailMerge.source") with { MenuPath = "tools/mailMerge", Surface = CommandSurface.Advanced, Execute = (_, ct) => ChooseSourceAsync(ct) },
        Editing("folio.mailMerge.insertField") with { MenuPath = "tools/mailMerge", Surface = CommandSurface.Advanced, RunsDocumentQueries = true, Execute = (_, ct) => InsertFieldAsync(ct) },
        Merging("folio.mailMerge.preview") with
        {
            CheckedWhen = _ => Editor is { } editor && records.Previewing(editor.Document),
            Execute = (_, _) => Run(editor => Preview(editor, !records.Previewing(editor.Document))),
        },
        Merging("folio.mailMerge.first") with { Execute = (_, _) => Run(editor => Move(editor, 0)) },
        Merging("folio.mailMerge.previous") with { Execute = (_, _) => Run(editor => Move(editor, records.Current(editor.Document) - 1)) },
        Merging("folio.mailMerge.next") with { Execute = (_, _) => Run(editor => Move(editor, records.Current(editor.Document) + 1)) },
        Merging("folio.mailMerge.last") with { Execute = (_, _) => Run(editor => Move(editor, int.MaxValue)) },
        Prototype("folio.mailMerge.run") with
        {
            MenuPath = "tools/mailMerge",
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && engine.Fields(editor.Document).Count > 0,
            RunsDocumentQueries = true,
            Execute = (_, ct) => MergeAsync(ct),
        },
    ];

    private CommandDefinition Merging(string id) =>
        Viewing(id) with
        {
            MenuPath = "tools/mailMerge",
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && records.Data(editor.Document) is { Rows.Count: > 0 },
        };

    private Task Run(Action<FolioEditor> action)
    {
        if (Editor is { } editor) action(editor);
        return Task.CompletedTask;
    }

    private async Task ChooseSourceAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        if (await sources.ChooseSourceAsync(ct).ConfigureAwait(true) is not { } choice) return;
        var loaded = await loader.LoadAsync(choice.Query with { MaxRows = 100_000 }, ct).ConfigureAwait(true);
        if (!loaded.Succeeded || loaded.Value is null)
        {
            status.Report(texts["folio.mailMerge.loadFailed"], NotificationSeverity.Error);
            return;
        }
        var data = reader.From(loaded.Value, editor.Services.Cultures.For(editor.Document.Language));
        documentQueries.Grant(editor.Document, new PulseOffice.Data.ExternalQueryRequest(choice.Query.ConnectionId, choice.Query.Sql));
        editor.Change(() => editor.Document.MergeSource = new MailMergeSource(choice.DisplayName, choice.Query.Sql, MergeCommandType.Command) { ConnectionId = choice.Query.ConnectionId });
        records.Load(editor.Document, data);
        status.Report(texts.Format("folio.mailMerge.loaded", data.Rows.Count, data.Columns.Count), NotificationSeverity.Success);
    }

    private async Task<MergeData?> DataAsync(FolioEditor editor, CancellationToken ct)
    {
        if (records.Data(editor.Document) is { } data) return data;
        if (editor.Document.MergeSource is not { ConnectionId: { Length: > 0 } connection } source) return null;
        if (!await documentQueries.AllowAsync(editor.Document, [new PulseOffice.Data.ExternalQueryRequest(connection, source.Command)], ct).ConfigureAwait(true))
        {
            status.Report(texts["status.query.declined"]);
            return null;
        }
        var loaded = await loader.LoadAsync(new PulseOffice.Document.Embedding.DataQuerySource { ConnectionId = connection, Sql = source.Command, MaxRows = 100_000 }, ct).ConfigureAwait(true);
        if (!loaded.Succeeded || loaded.Value is null) return null;
        data = reader.From(loaded.Value, editor.Services.Cultures.For(editor.Document.Language));
        records.Load(editor.Document, data);
        return data;
    }

    private async Task InsertFieldAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var data = await DataAsync(editor, ct).ConfigureAwait(true);
        string? column;
        if (data is { Columns.Count: > 0 })
        {
            var columns = data.Columns.ToList();
            var picked = await forms.AskAsync("command.folio.mailMerge.insertField.title", "folio.mailMerge.field.explanation",
                [new ChoiceField("column", texts["folio.mailMerge.column"], columns)], ct).ConfigureAwait(true);
            column = picked is null ? null : columns[picked.Choice("column", columns.Count)];
        }
        else
        {
            var typed = await forms.AskAsync("command.folio.mailMerge.insertField.title", "folio.mailMerge.field.noSource",
                [new TextField("column", texts["folio.mailMerge.column"], string.Empty)], ct).ConfigureAwait(true);
            column = typed?.Text("column").Trim();
        }
        if (string.IsNullOrEmpty(column)) return;
        using (editor.History.BeginTransaction("insert-field"))
        {
            var at = editor.Caret;
            editor.Insert((model, position) =>
            {
                at = position;
                model.InsertField(editor.Document, editor.History, position, TextFieldKind.Database, "«" + column + "»", column);
            }, "insert-field");
            refresh.UpdateFields(editor, new HashSet<string>(StringComparer.Ordinal) { at.ParagraphId });
        }
        editor.Refresh();
    }

    private void Preview(FolioEditor editor, bool on)
    {
        records.Preview(editor.Document, on);
        Refresh(editor);
    }

    private void Move(FolioEditor editor, int row)
    {
        records.Move(editor.Document, row);
        if (!records.Previewing(editor.Document)) records.Preview(editor.Document, true);
        Refresh(editor);
        status.Report(texts.Format("folio.mailMerge.record", records.Current(editor.Document) + 1, records.Data(editor.Document)?.Rows.Count ?? 0));
    }

    private void Refresh(FolioEditor editor) => refresh.ShowMergeRecord(editor);

    private async Task MergeAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        if (await DataAsync(editor, ct).ConfigureAwait(true) is not { Rows.Count: > 0 } data)
        {
            status.Report(texts["folio.mailMerge.noData"], NotificationSeverity.Warning);
            return;
        }
        if (await wizard.AskAsync(editor.Document, data, records.Current(editor.Document), ct).ConfigureAwait(true) is not { } plan) return;
        var target = plan.Target;
        var rows = Enumerable.Range(plan.From, plan.To - plan.From + 1).Where(row => row >= 0 && row < data.Rows.Count).ToList();
        var nameColumn = plan.NameColumn;
        var template = editor.Document;
        var stem = string.IsNullOrWhiteSpace(template.FileName) ? texts["document.defaultName"] : Path.GetFileNameWithoutExtension(template.FileName);

        if (target == MergeTarget.SingleDocument)
        {
            workspace.Open(documents.CreateFrom(output.Single(template, data, rows), texts.Format("folio.mailMerge.mergedTitle", stem)));
            status.Report(texts.Format("folio.mailMerge.done", rows.Count), NotificationSeverity.Success);
            return;
        }

        if (target == MergeTarget.SinglePdf)
        {
            var file = await files.SaveFileAsync(texts["command.folio.mailMerge.run.title"], stem + ".pdf", [new FileFilter("PDF", ["pdf"])], ct).ConfigureAwait(true);
            if (file is null) return;
            Report(await atomic.WriteAsync(file, stream => output.Pdf(template, data, rows, stream, stem), written => written.Succeeded, ct).ConfigureAwait(true));
            return;
        }

        var extension = target switch { MergeTarget.DocxFiles => "docx", MergeTarget.PdfFiles => "pdf", _ => "odt" };
        var first = await files.SaveFileAsync(texts["folio.mailMerge.folder"], stem + "." + extension, [new FileFilter(extension.ToUpperInvariant(), [extension])], ct).ConfigureAwait(true);
        if (first is null || Path.GetDirectoryName(first.Path) is not { Length: > 0 } folder) return;
        await first.DisposeAsync().ConfigureAwait(true);
        Report(output.Files(template, data, rows, target, folder, Path.GetFileNameWithoutExtension(first.Path), nameColumn));
    }

    private void Report(PulseOffice.Foundation.Diagnostics.OperationResult<int> result) =>
        status.Report(result.Succeeded ? texts.Format("folio.mailMerge.done", result.Value) : texts["folio.mailMerge.failed"],
            result.Succeeded ? NotificationSeverity.Success : NotificationSeverity.Error);
}
