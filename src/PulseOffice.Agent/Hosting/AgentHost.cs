using PulseOffice.Foundation.Modules;

namespace PulseOffice.Agent.Hosting;

public sealed record AgentDocumentInfo(string Id, ModuleKind Module, string Title, string? Path, bool IsModified, bool IsActive, bool IsReadOnly);

public sealed record AgentTarget(string? DocumentId, ModuleKind? Module);

public sealed record AgentRendering(byte[] Png, int PageCount, int Page);

public sealed class AgentHostException(string message) : Exception(message);

public interface IAgentWorkspace
{
    Task<IReadOnlyList<AgentDocumentInfo>> DocumentsAsync(CancellationToken ct);

    Task<AgentDocumentInfo> OpenAsync(string path, IReadOnlyDictionary<string, string> options, CancellationToken ct);

    Task<AgentDocumentInfo> CreateAsync(ModuleKind module, CancellationToken ct);

    Task<AgentDocumentInfo> ActivateAsync(string documentId, CancellationToken ct);

    Task<AgentDocumentInfo> SaveAsync(string documentId, CancellationToken ct);

    Task<AgentDocumentInfo> SaveAsAsync(string documentId, string path, CancellationToken ct);

    Task<string> ExportAsync(string documentId, string path, CancellationToken ct);

    Task<IReadOnlyList<string>> ExportFormatsAsync(string documentId, CancellationToken ct);

    Task<T> WithDocumentAsync<T>(AgentTarget target, string? undoLabel, Func<AgentDocumentInfo, CancellationToken, Task<T>> work, CancellationToken ct);

    Task<AgentRendering> RenderAsync(AgentTarget target, int page, string? range, int width, CancellationToken ct);
}

public sealed record AgentCommandInfo(string Id, string Title, string? Description, string? Category, string? CategoryKey, ModuleKind Module, bool IsEnabled, bool OutsideHistory = false, bool RunsDocumentQueries = false);

public enum AgentCommandOutcome { Ran, NotFound, Disabled, Failed, Refused }

public sealed record AgentCommandRun(AgentCommandOutcome Outcome, string? Reason = null);

public interface IAgentCommands
{
    Task<IReadOnlyList<AgentCommandInfo>> ListAsync(bool includeDisabled, CancellationToken ct);

    Task<AgentCommandRun> RunAsync(string id, string undoLabel, CancellationToken ct);
}

public sealed record AgentScriptOutcome(bool Succeeded, object? Value, string? Error);

public interface IAgentScripts
{
    Task<AgentScriptOutcome> RunAsync(ModuleKind module, string source, AgentAccessLevel access, CancellationToken ct);
}

public interface IAgentScriptCatalog
{
    PulseOffice.Scripting.ScriptApiCatalog Catalog { get; }
}

public enum AgentConfirmationKind { Overwrite, Irreversible, FileAccess, DocumentQueries }

public sealed record AgentConfirmationNeed(AgentConfirmationKind Kind, string Subject)
{
    public const int MaximumSubjectLength = 400;

    public string Subject { get; init; } = Subject is { Length: > MaximumSubjectLength }
        ? Subject[..(char.IsHighSurrogate(Subject[MaximumSubjectLength - 1]) ? MaximumSubjectLength - 1 : MaximumSubjectLength)] + "…"
        : Subject;

    public string Summary => $"{Kind.ToString().ToLowerInvariant()} {Subject}";
}

public sealed record AgentConfirmationRequest(string Tool, AgentConfirmationKind Kind, string Subject, string? Title = null)
{
    public string Summary => new AgentConfirmationNeed(Kind, Subject).Summary;
}

public interface IAgentConfirmation
{
    Task<bool> ConfirmAsync(AgentConfirmationRequest request, CancellationToken ct);
}

public interface IAgentFileSystem
{
    bool Exists(string path);
}

public sealed class AgentFileSystem : IAgentFileSystem
{
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
