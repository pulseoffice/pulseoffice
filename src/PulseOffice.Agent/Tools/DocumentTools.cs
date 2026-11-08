using System.Text.Json.Nodes;
using PulseOffice.Agent.Hosting;

namespace PulseOffice.Agent.Tools;

public sealed class DocumentToolSource(IAgentWorkspace workspace, IAgentModuleNames modules, IAgentFileSystem files) : IAgentToolSource
{
    public IEnumerable<AgentTool> Tools =>
    [
        new ListDocumentsTool(workspace, modules),
        new ActivateDocumentTool(workspace, modules),
        new CreateDocumentTool(workspace, modules),
        new OpenDocumentTool(workspace, modules),
        new SaveDocumentTool(workspace, modules, new AgentDocumentChoice(workspace)),
        new SaveDocumentAsTool(workspace, modules, files, new AgentDocumentChoice(workspace)),
        new ExportDocumentTool(workspace, files, new AgentDocumentChoice(workspace)),
    ];
}

public sealed class AgentDocumentJson(IAgentModuleNames modules)
{
    public JsonObject Describe(AgentDocumentInfo document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new JsonObject
        {
            ["id"] = document.Id,
            ["module"] = modules.Prefix(document.Module),
            ["title"] = document.Title,
            ["path"] = document.Path,
            ["modified"] = document.IsModified,
            ["active"] = document.IsActive,
            ["readOnly"] = document.IsReadOnly,
        };
    }
}

public sealed class AgentPaths
{
    public string Absolute(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new AgentArgumentException($"The path '{path}' must be absolute.");
        return Path.GetFullPath(path);
    }
}

internal sealed class ListDocumentsTool(IAgentWorkspace workspace, IAgentModuleNames modules) : AgentTool
{
    public override string Name => "list_documents";

    public override string Description => "Lists the documents open in PulseOffice, with id, module (folio, ledger, deck), title, path, modified and active flags.";

    public override AgentAccessLevel Access => AgentAccessLevel.Read;

    public override JsonObject InputSchema() => new SchemaBuilder().Build();

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var json = new AgentDocumentJson(modules);
        var documents = await workspace.DocumentsAsync(ct).ConfigureAwait(true);
        return AgentToolResult.Json(new JsonArray([.. documents.Select(document => (JsonNode)json.Describe(document))]));
    }
}

internal sealed class ActivateDocumentTool(IAgentWorkspace workspace, IAgentModuleNames modules) : AgentTool
{
    public override string Name => "activate_document";

    public override string Description => "Brings an open document to the front, so it becomes the active one of its module.";

    public override AgentAccessLevel Access => AgentAccessLevel.Read;

    public override JsonObject InputSchema() => new SchemaBuilder().String("document", "Id from list_documents.", required: true).Build();

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct) =>
        AgentToolResult.Json(new AgentDocumentJson(modules).Describe(await workspace.ActivateAsync(arguments.Text("document"), ct).ConfigureAwait(true)));
}

internal sealed class CreateDocumentTool(IAgentWorkspace workspace, IAgentModuleNames modules) : AgentTool
{
    public override string Name => "create_document";

    public override string Description => "Creates a new empty document in Folio (text), Ledger (spreadsheet) or Deck (presentation) and makes it active.";

    public override AgentAccessLevel Access => AgentAccessLevel.Edit;

    public override JsonObject InputSchema() =>
        new SchemaBuilder().String("module", "folio, ledger or deck.", required: true, modules.Names).Build();

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var module = modules.Parse(arguments.Text("module"))!.Value;
        return AgentToolResult.Json(new AgentDocumentJson(modules).Describe(await workspace.CreateAsync(module, ct).ConfigureAwait(true)));
    }
}

internal sealed class OpenDocumentTool(IAgentWorkspace workspace, IAgentModuleNames modules) : AgentTool
{
    public override string Name => "open_document";

    public override string Description =>
        "Opens a file (ODF, OOXML and the other formats PulseOffice reads) from an absolute path. " +
        "Delimited text (.csv, .tsv, .txt) opens with the options detected from the file, without asking the user; the csv_* parameters override them.";

    public override AgentAccessLevel Access => AgentAccessLevel.Files;

    private readonly string[] _options = ["csv_separator", "csv_encoding", "csv_language", "csv_skip_rows"];

    public override JsonObject InputSchema() => new SchemaBuilder()
        .String("path", "Absolute path of the file.", required: true)
        .String("csv_separator", "Field separator of delimited text: comma, semicolon, tab, space or a single character.")
        .String("csv_encoding", "Encoding of delimited text: utf-8, windows-1252, iso-8859-15 or utf-16.")
        .String("csv_language", "Culture for numbers and dates in delimited text, for example en-US or it-IT.")
        .String("csv_skip_rows", "Number of leading lines of delimited text to skip.")
        .Build();

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var options = _options
            .Select(name => (Name: name, Value: arguments.OptionalText(name)))
            .Where(option => option.Value is { Length: > 0 })
            .ToDictionary(option => option.Name, option => option.Value!, StringComparer.Ordinal);
        return AgentToolResult.Json(new AgentDocumentJson(modules).Describe(
            await workspace.OpenAsync(new AgentPaths().Absolute(arguments.Text("path")), options, ct).ConfigureAwait(true)));
    }
}

internal sealed class SaveDocumentTool(IAgentWorkspace workspace, IAgentModuleNames modules, AgentDocumentChoice choice) : AgentTool
{
    public override string Name => "save_document";

    public override string Description => "Saves a document over its own file. Asks the user to confirm, because the file on disk is overwritten.";

    public override AgentAccessLevel Access => AgentAccessLevel.Files;

    public override AgentToolAnnotations Annotations => new(false, true);

    public override JsonObject InputSchema() => new SchemaBuilder().String("document", "Id from list_documents; may be omitted when only one document is open.").Build();

    public override async Task<AgentConfirmationNeed?> ConfirmationAsync(AgentArguments arguments, CancellationToken ct)
    {
        var id = await choice.IdAsync(arguments, ct).ConfigureAwait(true);
        var document = (await workspace.DocumentsAsync(ct).ConfigureAwait(true)).FirstOrDefault(candidate => candidate.Id == id)
            ?? throw new AgentHostException($"No open document has the id '{id}'.");
        return document.Path is { } path
            ? new AgentConfirmationNeed(AgentConfirmationKind.Overwrite, path)
            : throw new AgentHostException("The document has never been saved: use save_document_as with a path.");
    }

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct) =>
        AgentToolResult.Json(new AgentDocumentJson(modules).Describe(await workspace.SaveAsync(await choice.IdAsync(arguments, ct).ConfigureAwait(true), ct).ConfigureAwait(true)));
}

internal sealed class SaveDocumentAsTool(IAgentWorkspace workspace, IAgentModuleNames modules, IAgentFileSystem files, AgentDocumentChoice choice) : AgentTool
{
    public override string Name => "save_document_as";

    public override string Description =>
        "Saves a document to a new absolute path; the extension chooses the format (.odt/.ods/.odp native, .docx/.xlsx/.pptx Office). " +
        "If the file exists the user must confirm the overwrite.";

    public override AgentAccessLevel Access => AgentAccessLevel.Files;

    public override AgentToolAnnotations Annotations => new(false, true);

    public override JsonObject InputSchema() => new SchemaBuilder()
        .String("document", "Id from list_documents; may be omitted when only one document is open.")
        .String("path", "Absolute destination path.", required: true)
        .Build();

    public override Task<AgentConfirmationNeed?> ConfirmationAsync(AgentArguments arguments, CancellationToken ct)
    {
        var path = new AgentPaths().Absolute(arguments.Text("path"));
        return Task.FromResult(files.Exists(path) ? new AgentConfirmationNeed(AgentConfirmationKind.Overwrite, path) : null);
    }

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct) =>
        AgentToolResult.Json(new AgentDocumentJson(modules).Describe(
            await workspace.SaveAsAsync(await choice.IdAsync(arguments, ct).ConfigureAwait(true), new AgentPaths().Absolute(arguments.Text("path")), ct).ConfigureAwait(true)));
}

internal sealed class ExportDocumentTool(IAgentWorkspace workspace, IAgentFileSystem files, AgentDocumentChoice choice) : AgentTool
{
    public override string Name => "export_document";

    public override string Description =>
        "Exports a copy of a document to an absolute path without changing the document's own file. " +
        "The extension chooses the format: .pdf, or the native and Office formats of the module. If the file exists the user must confirm.";

    public override AgentAccessLevel Access => AgentAccessLevel.Files;

    public override AgentToolAnnotations Annotations => new(false, true);

    public override JsonObject InputSchema() => new SchemaBuilder()
        .String("document", "Id from list_documents; may be omitted when only one document is open.")
        .String("path", "Absolute destination path, for example /Users/me/report.pdf.", required: true)
        .Build();

    public override Task<AgentConfirmationNeed?> ConfirmationAsync(AgentArguments arguments, CancellationToken ct)
    {
        var path = new AgentPaths().Absolute(arguments.Text("path"));
        return Task.FromResult(files.Exists(path) ? new AgentConfirmationNeed(AgentConfirmationKind.Overwrite, path) : null);
    }

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var written = await workspace.ExportAsync(await choice.IdAsync(arguments, ct).ConfigureAwait(true), new AgentPaths().Absolute(arguments.Text("path")), ct).ConfigureAwait(true);
        return AgentToolResult.Json(new JsonObject { ["path"] = written });
    }
}
