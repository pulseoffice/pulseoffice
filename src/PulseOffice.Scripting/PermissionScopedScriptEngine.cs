using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting;

public sealed class PermissionScopedScriptEngine(IScriptEngine inner, IScriptExecutionPermissions permissions) : IScriptEngine
{
    public ScriptLanguage Language => inner.Language;

    public string DisplayName => inner.DisplayName;

    public string RuntimeVersion => inner.RuntimeVersion;

    public bool IsSandboxed => inner.IsSandboxed;

    public IReadOnlySet<string> Keywords => inner.Keywords;

    public IReadOnlyList<ScriptDiagnostic> Analyze(string source) => inner.Analyze(source);

    public async Task<ScriptResult> ExecuteAsync(string source, ScriptContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var scope = permissions.Enter(context.Permissions);
        return await inner.ExecuteAsync(source, context, ct).ConfigureAwait(true);
    }

    public IReadOnlyList<CompletionItem> Complete(string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module) =>
        inner.Complete(source, position, api, module);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
