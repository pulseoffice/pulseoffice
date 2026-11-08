using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting;

public readonly record struct ScriptPosition(int Line, int Column);

public enum ScriptDiagnosticSeverity { Hint, Info, Warning, Error }

public sealed record ScriptDiagnostic(
    ScriptDiagnosticSeverity Severity,
    string Message,
    ScriptPosition Start,
    ScriptPosition End,
    string? Code = null)
{
    public string? MessageKey { get; init; }

    public IReadOnlyList<object> MessageArguments { get; init; } = [];
}

public sealed record ScriptResult(
    bool Succeeded,
    object? Value,
    IReadOnlyList<ScriptDiagnostic> Diagnostics,
    TimeSpan Elapsed,
    string? StackTrace = null);

public sealed record ScriptPermissions
{
    public bool DocumentAccess { get; init; } = true;

    public bool FileSystemRead { get; init; }
    public bool FileSystemWrite { get; init; }

    public bool MacroFolderWrite { get; init; }
    public bool Network { get; init; }
    public bool LaunchProcesses { get; init; }

    public bool DatabaseAccess { get; init; }

    public bool AllowsUnsandboxedEngines { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public long MaxMemoryBytes { get; init; } = 256L * 1024 * 1024;

    public static readonly ScriptPermissions Sandboxed = new();

    public static readonly ScriptPermissions Trusted = new()
    {
        FileSystemRead = true,
        FileSystemWrite = true,
        Network = true,
        DatabaseAccess = true,
        Timeout = TimeSpan.FromMinutes(5),
    };
}

public sealed record ScriptContext
{
    public required ModuleKind Module { get; init; }
    public required IReadOnlyDictionary<string, object> Globals { get; init; }
    public ScriptPermissions Permissions { get; init; } = ScriptPermissions.Sandboxed;

    public Action<string>? OnOutput { get; init; }
}

public interface IScriptEngine : IAsyncDisposable
{
    ScriptLanguage Language { get; }
    string DisplayName { get; }

    string RuntimeVersion { get; }

    bool IsSandboxed { get; }

    IReadOnlyList<ScriptDiagnostic> Analyze(string source);

    Task<ScriptResult> ExecuteAsync(string source, ScriptContext context, CancellationToken ct = default);

    IReadOnlyList<CompletionItem> Complete(string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module);

    IReadOnlySet<string> Keywords { get; }
}

public enum CompletionKind { Keyword, Variable, Function, Method, Property, Class, Module, Enum, Snippet }

public sealed record CompletionItem(
    string Label,
    CompletionKind Kind,
    string? Detail = null,
    string? Documentation = null,
    string? InsertText = null)
{
    public bool IsApiSymbol { get; init; }
}

public interface IModuleScriptGlobals
{
    ModuleKind Module { get; }

    IReadOnlyDictionary<string, object> Globals { get; }
}
