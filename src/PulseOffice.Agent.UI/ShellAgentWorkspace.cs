using PulseOffice.Agent.Hosting;
using PulseOffice.Formats.Pdf;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;

namespace PulseOffice.Agent.UI;

public sealed record ShellAgentServices(
    IDocumentWorkspace Workspace,
    IDocumentOpenRouter Router,
    IDocumentFileServices Files,
    IDocumentOperationsCatalog Operations,
    IPdfExporter Pdf,
    IAtomicFileWriter Atomic,
    CommandRegistry Registry,
    Func<CommandExecutor> Executor,
    IAgentDocumentIds Ids,
    IAgentUiThread Ui,
    IAgentPageRenderer Renderer,
    ITextCatalog Texts,
    IUnattendedOpening Unattended,
    ICommandEffectChecks Effects,
    IAgentOperationGate Gate);

public sealed class ShellAgentWorkspace(ShellAgentServices services) : IAgentWorkspace
{
    private IDocumentWorkspace Workspace => services.Workspace;

    private Task<T> Exclusive<T>(Func<Task<T>> work, CancellationToken ct) =>
        services.Ui.InvokeAsync(() => services.Gate.RunAsync(work, ct));

    private Task<T> Editing<T>(Func<Task<T>> work, CancellationToken ct) =>
        services.Ui.InvokeAsync(() => services.Gate.EditAsync(work, ct));

    public Task<IReadOnlyList<AgentDocumentInfo>> DocumentsAsync(CancellationToken ct) =>
        services.Ui.InvokeAsync(() => Task.FromResult<IReadOnlyList<AgentDocumentInfo>>([.. Workspace.Documents.Select(Describe)]));

    public Task<AgentDocumentInfo> OpenAsync(string path, IReadOnlyDictionary<string, string> options, CancellationToken ct) => Editing(async () =>
    {
        if (Directory.Exists(path)) throw new AgentHostException($"'{path}' is a folder: give the path of a file.");
        if (!File.Exists(path)) throw new AgentHostException($"The file '{path}' does not exist.");
        if (!services.Router.CanOpen(path)) throw new AgentHostException($"PulseOffice cannot open '{Path.GetFileName(path)}'.");

        using (services.Unattended.Begin(options))
            await services.Router.EditAsync(path, ct).ConfigureAwait(true);
        var opened = services.Router.FindOpen(path)
            ?? throw new AgentHostException($"'{Path.GetFileName(path)}' could not be opened: see the status bar of PulseOffice.");
        Workspace.Activate(opened);
        return Describe(opened);
    }, ct);

    public Task<AgentDocumentInfo> CreateAsync(ModuleKind module, CancellationToken ct) => Editing(async () =>
    {
        var id = new CommandId($"shell.file.new.{module.ToString().ToLowerInvariant()}");
        var command = services.Registry.Find(id) ?? throw new AgentHostException($"No command creates a {module} document.");
        var before = Workspace.Documents.Count;
        await services.Executor().ExecuteAsync(command, Workspace.Context, ct).ConfigureAwait(true);
        if (Workspace.Documents.Count == before || Workspace.Active is not { } created)
            throw new AgentHostException("The new document could not be created.");
        return Describe(created);
    }, ct);

    public Task<AgentDocumentInfo> ActivateAsync(string documentId, CancellationToken ct) => Editing(() =>
    {
        var document = Find(documentId);
        Workspace.Activate(document);
        return Task.FromResult(Describe(document));
    }, ct);

    public Task<AgentDocumentInfo> SaveAsync(string documentId, CancellationToken ct) => Editing(() => Holding(Find(documentId), async document =>
    {
        if (document.Path is null) throw new AgentHostException("The document has never been saved: use save_document_as.");
        var files = services.Files.For(document) ?? throw new AgentHostException("This document cannot be saved.");
        await files.SaveAsync(document, ct).ConfigureAwait(true);
        if (document.IsModified) throw new AgentHostException("The document was not saved: see the status bar of PulseOffice.");
        return Describe(document);
    }), ct);

    public Task<AgentDocumentInfo> SaveAsAsync(string documentId, string path, CancellationToken ct) => Editing(() => Holding(Find(documentId), async document =>
    {
        var files = services.Files.For(document) ?? throw new AgentHostException("This document cannot be saved.");
        if (services.Operations.For(document) is { } operations
            && !string.Equals(Path.GetExtension(path), "." + operations.NativeExtension, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetExtension(path), "." + operations.OfficeExtension, StringComparison.OrdinalIgnoreCase))
            throw new AgentHostException($"'{Path.GetExtension(path)}' is not a format this document is saved in; use .{operations.NativeExtension} or .{operations.OfficeExtension}.");
        if (!await files.SaveToAsync(document, path, ct).ConfigureAwait(true))
            throw new AgentHostException($"The document was not saved to '{path}': see the status bar of PulseOffice.");
        return Describe(document);
    }), ct);

    public Task<string> ExportAsync(string documentId, string path, CancellationToken ct) => Exclusive(() => Holding(Find(documentId), document =>
    {
        services.Gate.CommitPendingEdits(document);
        var operations = services.Operations.For(document) ?? throw new AgentHostException("This document cannot be exported.");

        if (string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var printable = operations.BuildExportable(document, false) ?? throw new AgentHostException("The document has nothing to export.");
            var properties = operations.Properties(document);
            var options = new PdfExportOptions(properties?.Title ?? operations.SuggestedName(document), properties?.Author ?? string.Empty, properties?.Subject ?? string.Empty)
            {
                Bookmarks = operations.PdfExport(document).HasHeadings,
            };
            var written = services.Atomic.Replace(path, stream => services.Pdf.Write(printable, stream, options), result => result.Succeeded);
            return written.Succeeded
                ? Task.FromResult(path)
                : throw new AgentHostException($"The PDF export failed: {written.Diagnostics.FirstOrDefault()?.Message}");
        }

        return operations.WriteTo(document, path)
            ? Task.FromResult(path)
            : throw new AgentHostException(
                $"'{Path.GetExtension(path)}' is not a format this document can be exported to; use .pdf, .{operations.NativeExtension} or .{operations.OfficeExtension}.");
    }), ct);

    public Task<IReadOnlyList<string>> ExportFormatsAsync(string documentId, CancellationToken ct) => services.Ui.InvokeAsync(() =>
    {
        var operations = services.Operations.For(Find(documentId)) ?? throw new AgentHostException("This document cannot be exported.");
        return Task.FromResult<IReadOnlyList<string>>(["pdf", operations.NativeExtension, operations.OfficeExtension]);
    });

    public Task<T> WithDocumentAsync<T>(AgentTarget target, string? undoLabel, Func<AgentDocumentInfo, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        Func<Func<Task<T>>, CancellationToken, Task<T>> turn = undoLabel is null ? Exclusive : Editing;
        return turn(() => Holding(Resolve(target), async document =>
        {
            if (undoLabel is not null && document.IsReadOnly) throw new AgentHostException("The document is read-only.");

            if (undoLabel is null) return await work(Describe(document), ct).ConfigureAwait(true);

            var effects = services.Effects.Open(_agentEdit);
            T result;
            try
            {
                using (document.History.BeginLabelledStep(services.Texts.Format("agent.history", undoLabel)))
                    result = await work(Describe(document), ct).ConfigureAwait(true);
            }
            finally
            {
                effects.Finish();
            }

            if (effects.Refused) throw new AgentHostException("The change touched protected content and was undone. Unprotect it first.");
            return result;
        }), ct);
    }

    private readonly CommandDefinition _agentEdit = new()
    {
        Id = new CommandId("agent.edit"),
        Execute = (_, _) => Task.CompletedTask,
    };

    public Task<AgentRendering> RenderAsync(AgentTarget target, int page, string? range, int width, CancellationToken ct) =>
        Exclusive(() => Holding(Resolve(target), document =>
        {
            services.Gate.CommitPendingEdits(document);
            var operations = services.Operations.For(document) ?? throw new AgentHostException("This document cannot be drawn.");

            IPrintableDocument? printable;
            if (range is { Length: > 0 })
            {
                if (document.Module != ModuleKind.Sheets) throw new AgentHostException("'range' works only with Ledger documents.");
                printable = operations.BuildRange(document, range) ?? throw new AgentHostException($"'{range}' is not a valid range or it is empty.");
            }
            else
            {
                printable = operations.BuildPages(document) ?? throw new AgentHostException("The document has no page to draw.");
            }

            if (page > printable.PageCount) throw new AgentHostException($"There is no page {page}: the document has {printable.PageCount} pages.");
            return Task.FromResult(new AgentRendering(services.Renderer.Render(printable, page - 1, width), printable.PageCount, page));
        }), ct);

    private Task<T> Holding<T>(OpenDocument document, Func<OpenDocument, Task<T>> work) =>
        services.Gate.HoldingAsync(document, () => work(document));

    private OpenDocument Find(string documentId) =>
        Workspace.Documents.FirstOrDefault(document => services.Ids.IdOf(document) == documentId)
        ?? throw new AgentHostException($"No open document has the id '{documentId}': see list_documents.");

    private OpenDocument Resolve(AgentTarget target)
    {
        if (target.DocumentId is { Length: > 0 } id)
        {
            var found = Find(id);
            return target.Module is { } wanted && found.Module != wanted
                ? throw new AgentHostException($"The document '{id}' is not a {ModuleIdentity.For(wanted).BrandName} document.")
                : found;
        }

        if (target.Module is not { } module)
            return Workspace.Active ?? throw new AgentHostException("No document is open: use create_document or open_document.");

        if (Workspace.Active is { } active && active.Module == module) return active;
        return Workspace.Documents.LastOrDefault(document => document.Module == module)
            ?? throw new AgentHostException($"No {ModuleIdentity.For(module).BrandName} document is open: use create_document or open_document.");
    }

    private AgentDocumentInfo Describe(OpenDocument document) =>
        new(services.Ids.IdOf(document), document.Module, document.Title, document.Path, document.IsModified,
            ReferenceEquals(Workspace.Active, document), document.IsReadOnly);
}
