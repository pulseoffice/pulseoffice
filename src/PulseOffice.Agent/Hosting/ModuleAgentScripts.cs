using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;

namespace PulseOffice.Agent.Hosting;

public sealed class ModuleAgentScripts(
    IEnumerable<IScriptEngine> engines,
    IEnumerable<IModuleScriptGlobals> globals,
    IScriptExecutionPermissions permissions) : IAgentScripts
{
    private readonly IReadOnlyList<IScriptEngine> _engines = [.. engines];
    private readonly IReadOnlyList<IModuleScriptGlobals> _globals = [.. globals];

    public async Task<AgentScriptOutcome> RunAsync(ModuleKind module, string source, AgentAccessLevel access, CancellationToken ct)
    {
        var engine = _engines.FirstOrDefault(candidate => candidate.Language == ScriptLanguage.JavaScript)
            ?? throw new AgentHostException("The JavaScript engine is not available.");
        var scope = _globals.FirstOrDefault(candidate => candidate.Module == module)
            ?? throw new AgentHostException($"The {module} module has no script objects.");

        var result = await new PermissionScopedScriptEngine(engine, permissions).ExecuteAsync(source, new ScriptContext
        {
            Module = module,
            Globals = scope.Globals.ToDictionary(pair => pair.Key, pair => pair.Value),
            Permissions = PermissionsFor(access),
        }, ct).ConfigureAwait(true);

        return new AgentScriptOutcome(result.Succeeded, result.Value,
            result.Succeeded ? null : string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    private static ScriptPermissions PermissionsFor(AgentAccessLevel access) => access == AgentAccessLevel.Files
        ? ScriptPermissions.Sandboxed with { FileSystemRead = true, FileSystemWrite = true }
        : ScriptPermissions.Sandboxed;
}
