using Microsoft.Extensions.Logging;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting.Editor;

public sealed class LoggedScriptEngine(IScriptEngine inner, ILogger logger) : IScriptEngine
{
    public ScriptLanguage Language => inner.Language;

    public string DisplayName => inner.DisplayName;

    public string RuntimeVersion => inner.RuntimeVersion;

    public bool IsSandboxed => inner.IsSandboxed;

    public IReadOnlySet<string> Keywords => inner.Keywords;

    public IReadOnlyList<ScriptDiagnostic> Analyze(string source) => Logged(inner.Analyze(source));

    public async Task<ScriptResult> ExecuteAsync(string source, ScriptContext context, CancellationToken ct = default)
    {
        var result = await inner.ExecuteAsync(source, context, ct).ConfigureAwait(true);
        Logged(result.Diagnostics);
        if (!result.Succeeded && result.StackTrace is { Length: > 0 } trace)
            logger.LogInformation("Macro stack trace ({Engine}): {Trace}", inner.DisplayName, trace);
        return result;
    }

    public IReadOnlyList<CompletionItem> Complete(string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module) =>
        inner.Complete(source, position, api, module);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private IReadOnlyList<ScriptDiagnostic> Logged(IReadOnlyList<ScriptDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics.Where(diagnostic => diagnostic.Severity == ScriptDiagnosticSeverity.Error))
            logger.LogInformation("Macro {Code} ({Engine}) at {Line}:{Column}: {Detail}",
                diagnostic.Code, inner.DisplayName, diagnostic.Start.Line, diagnostic.Start.Column, diagnostic.Message);
        return diagnostics;
    }
}
