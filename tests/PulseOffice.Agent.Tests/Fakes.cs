using System.Text.Json.Nodes;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Tools;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;

namespace PulseOffice.Agent.Tests;

internal sealed class FakePreferences : IAgentPreferences
{
    public bool Enabled { get; set; } = true;

    public int Port { get; set; } = 47820;

    public AgentAccessLevel Level { get; set; } = AgentAccessLevel.Files;

    public event EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeConfirmation : IAgentConfirmation
{
    public bool Answer { get; set; } = true;

    public List<AgentConfirmationRequest> Asked { get; } = [];

    public Task<bool> ConfirmAsync(AgentConfirmationRequest request, CancellationToken ct)
    {
        Asked.Add(request);
        return Task.FromResult(Answer);
    }
}

internal sealed class MemoryLog : IAgentOperationLog
{
    private readonly List<AgentOperation> _operations = [];

    public IReadOnlyList<AgentOperation> Recent => [.. _operations];

    public event EventHandler? Changed;

    public void Record(AgentOperation operation)
    {
        _operations.Insert(0, operation);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeFiles : IAgentFileSystem
{
    public HashSet<string> Existing { get; } = [];

    public bool Exists(string path) => Existing.Contains(path);
}

internal sealed class FakeWorkspace : IAgentWorkspace
{
    private int _next = 1;

    public List<AgentDocumentInfo> Documents { get; } = [];

    public List<string> UndoSteps { get; } = [];

    public List<string> Exported { get; } = [];

    public byte[] Picture { get; } = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10];

    public AgentDocumentInfo Add(ModuleKind module, string title, string? path = null)
    {
        var document = new AgentDocumentInfo($"d{_next++}", module, title, path, false, true, false);
        for (var i = 0; i < Documents.Count; i++) Documents[i] = Documents[i] with { IsActive = false };
        Documents.Add(document);
        return document;
    }

    public Task<IReadOnlyList<AgentDocumentInfo>> DocumentsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AgentDocumentInfo>>([.. Documents]);

    public Task<AgentDocumentInfo> OpenAsync(string path, IReadOnlyDictionary<string, string> options, CancellationToken ct) =>
        Task.FromResult(Add(ModuleKind.Documents, Path.GetFileNameWithoutExtension(path), path));

    public Task<AgentDocumentInfo> CreateAsync(ModuleKind module, CancellationToken ct) => Task.FromResult(Add(module, "Untitled"));

    public Task<AgentDocumentInfo> ActivateAsync(string documentId, CancellationToken ct) => Task.FromResult(Find(documentId));

    public Task<AgentDocumentInfo> SaveAsync(string documentId, CancellationToken ct) => Task.FromResult(Find(documentId));

    public Task<AgentDocumentInfo> SaveAsAsync(string documentId, string path, CancellationToken ct) => Task.FromResult(Find(documentId) with { Path = path });

    public Task<string> ExportAsync(string documentId, string path, CancellationToken ct)
    {
        Find(documentId);
        Exported.Add(path);
        return Task.FromResult(path);
    }

    public Task<IReadOnlyList<string>> ExportFormatsAsync(string documentId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(["pdf"]);

    public async Task<T> WithDocumentAsync<T>(AgentTarget target, string? undoLabel, Func<AgentDocumentInfo, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        var document = target.DocumentId is { } id
            ? Find(id)
            : Documents.LastOrDefault(candidate => target.Module is null || candidate.Module == target.Module)
              ?? throw new AgentHostException("No document of that module is open.");
        if (undoLabel is not null) UndoSteps.Add(undoLabel);
        return await work(document, ct).ConfigureAwait(false);
    }

    public Task<AgentRendering> RenderAsync(AgentTarget target, int page, string? range, int width, CancellationToken ct) =>
        Task.FromResult(new AgentRendering(Picture, 3, page));

    private AgentDocumentInfo Find(string id) =>
        Documents.FirstOrDefault(document => document.Id == id) ?? throw new AgentHostException($"No open document has the id '{id}'.");
}

internal sealed class FakeCommands : IAgentCommands
{
    public List<AgentCommandInfo> Commands { get; } =
    [
        new("ledger.format.bold", "Bold", "Makes the text bold", "Format", "format", ModuleKind.Sheets, true),
        new("ledger.sheet.delete", "Delete sheet", null, "Sheet", "sheet", ModuleKind.Sheets, true),
        new("shell.file.exportPdf", "Export as PDF", null, "File", "file", ModuleKind.Shell, true),
        new("shell.tools.options", "Settings", null, "Tools", "tools", ModuleKind.Shell, true),
        new("ledger.data.sort", "Sort", null, "Data", "data", ModuleKind.Sheets, false),
    ];

    public List<string> Ran { get; } = [];

    public Task<IReadOnlyList<AgentCommandInfo>> ListAsync(bool includeDisabled, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AgentCommandInfo>>([.. Commands.Where(command => includeDisabled || command.IsEnabled)]);

    public string? Refusal { get; set; }

    public Task<AgentCommandRun> RunAsync(string id, string undoLabel, CancellationToken ct)
    {
        var command = Commands.FirstOrDefault(candidate => candidate.Id == id);
        if (command is null) return Task.FromResult(new AgentCommandRun(AgentCommandOutcome.NotFound));
        if (!command.IsEnabled) return Task.FromResult(new AgentCommandRun(AgentCommandOutcome.Disabled));
        if (Refusal is { } reason) return Task.FromResult(new AgentCommandRun(AgentCommandOutcome.Refused, reason));
        Ran.Add(id);
        return Task.FromResult(new AgentCommandRun(AgentCommandOutcome.Ran));
    }
}

public sealed class FakeSheet(PulseOffice.Scripting.IScriptExecutionPermissions permissions)
{
    private readonly Dictionary<string, object?> _cells = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "Sheet1";

    public string Selection => "A1";

    public void Write(string reference, object? value) => _cells[reference] = value;

    public string Read(string reference) =>
        _cells.TryGetValue(reference, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    public void WriteRange(string reference, object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _cells[reference] = values.Length;
    }

    public bool Tracking { get; private set; }

    public bool SetTracking(bool on) => Tracking = on;

    public bool ExportPdf(string path)
    {
        permissions.Demand(PulseOffice.Scripting.ScriptCapability.FileSystemWrite);
        return path.Length > 0;
    }
}

internal sealed class FakeGlobals(FakeSheet sheet) : IModuleScriptGlobals
{
    public ModuleKind Module => ModuleKind.Sheets;

    public IReadOnlyDictionary<string, object> Globals { get; } = new Dictionary<string, object> { ["sheet"] = sheet };
}

internal sealed class FakeCatalog : IAgentScriptCatalog
{
    public ScriptApiCatalog Catalog { get; } = Build();

    private static ScriptApiCatalog Build()
    {
        var catalog = new ScriptApiCatalog();
        ApiSymbol Member(string name, ApiSymbolKind kind, params string[] parameters) => new()
        {
            Name = name,
            Kind = kind,
            QualifiedName = "sheet." + name,
            Module = ModuleKind.Sheets,
            Signature = kind == ApiSymbolKind.Method ? $"{name}({string.Join(", ", parameters)})" : name,
            Documentation = name + " documentation",
            Title = name + " title",
            Parameters = [.. parameters.Select(parameter => new ApiParameter(parameter, string.Empty, Documentation: parameter + " doc"))],
        };

        catalog.Register(new ApiSymbol
        {
            Name = "sheet",
            Kind = ApiSymbolKind.Class,
            QualifiedName = "sheet",
            Module = ModuleKind.Sheets,
            Documentation = "The active sheet.",
            Title = "Read sheet properties",
            Members =
            [
                Member("Name", ApiSymbolKind.Property),
                Member("Selection", ApiSymbolKind.Property),
                Member("Read", ApiSymbolKind.Method, "reference"),
                Member("Write", ApiSymbolKind.Method, "reference", "value"),
                Member("WriteRange", ApiSymbolKind.Method, "reference", "values"),
                Member("ExportPdf", ApiSymbolKind.Method, "path"),
                Member("Log", ApiSymbolKind.Method, "message"),
                Member("SetTracking", ApiSymbolKind.Method, "on") with { IsOutsideHistory = true },
            ],
        });
        catalog.Register(new ApiSymbol { Name = "app", Kind = ApiSymbolKind.Class, QualifiedName = "app", Module = ModuleKind.Shell });
        return catalog;
    }
}

internal sealed class Harness
{
    public FakePreferences Preferences { get; } = new();

    public FakeConfirmation Confirmation { get; } = new();

    public MemoryLog Log { get; } = new();

    public FakeWorkspace Workspace { get; } = new();

    public FakeCommands Commands { get; } = new();

    public FakeFiles Files { get; } = new();

    public PulseOffice.Scripting.ScriptExecutionPermissions Permissions { get; } = new();

    public FakeSheet Sheet { get; }

    public AgentModuleNames Modules { get; } = new();

    public IAgentToolCatalog Catalog { get; }

    public AgentToolInvoker Invoker { get; }

    public Harness()
    {
        Sheet = new FakeSheet(Permissions);
        Catalog = new AgentToolCatalog(
        [
            new DocumentToolSource(Workspace, Modules, Files),
            new CommandToolSource(Commands, new AgentCommandPolicy(), Modules, Preferences),
            new ScriptCatalogToolSource(new FakeCatalog(), Workspace,
                new ModuleAgentScripts([new PulseOffice.Scripting.JavaScript.JavaScriptEngine()], [new FakeGlobals(Sheet)], Permissions),
                new ScriptMemberAccess(), Modules),
            new ViewToolSource(Workspace, Modules),
        ]);
        Invoker = new AgentToolInvoker(Preferences, Confirmation, Log, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentToolInvoker>.Instance);
    }

    public Task<AgentToolResult> CallAsync(string tool, JsonObject? arguments = null) =>
        Invoker.InvokeAsync(Catalog.Find(tool) ?? throw new InvalidOperationException(tool), arguments ?? [], "test", CancellationToken.None);
}

internal sealed class FakePrivateFiles : PulseOffice.Platform.IPrivateFiles
{
    public List<string> Folders { get; } = [];

    public List<string> Restricted { get; } = [];

    public void CreateFolder(string folder)
    {
        Folders.Add(folder);
        Directory.CreateDirectory(folder);
    }

    public FileStream Open(string path, FileMode mode, FileShare share = FileShare.None)
    {
        Restricted.Add(path);
        return new FileStream(path, mode, FileAccess.Write, share);
    }

    public void Restrict(string path) => Restricted.Add(path);
}
