using System.Text.Json;
using System.Text.Json.Nodes;
using PulseOffice.Agent.Hosting;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;

namespace PulseOffice.Agent.Tools;

public interface IScriptMemberAccess
{
    bool IsExposed(ApiSymbol member);

    AgentAccessLevel AccessOf(ApiSymbol member);
}

public sealed class ScriptMemberAccess : IScriptMemberAccess
{
    private readonly IReadOnlySet<string> _hidden = new HashSet<string>(StringComparer.Ordinal) { "Undo", "Redo", "Log" };

    private readonly IReadOnlySet<string> _readers = new HashSet<string>(StringComparer.Ordinal)
    {
        "Read", "Number", "Formula", "FormulaLocal", "IsEmpty", "Range", "Numbers", "DistinctValues", "Find",
        "Describe", "Values", "RangeOf", "Paragraph", "Style", "Styles", "Cell", "Bookmarks", "Title", "Body", "Notes",
    };

    public bool IsExposed(ApiSymbol member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.Kind is ApiSymbolKind.Method or ApiSymbolKind.Property && !_hidden.Contains(member.Name);
    }

    public AgentAccessLevel AccessOf(ApiSymbol member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member.Parameters.Any(parameter => parameter.Name.Contains("path", StringComparison.OrdinalIgnoreCase))) return AgentAccessLevel.Files;
        return member.Kind == ApiSymbolKind.Property || _readers.Contains(member.Name) ? AgentAccessLevel.Read : AgentAccessLevel.Edit;
    }
}

public sealed class ScriptCatalogToolSource(
    IAgentScriptCatalog catalog,
    IAgentWorkspace workspace,
    IAgentScripts scripts,
    IScriptMemberAccess access,
    IAgentModuleNames modules) : IAgentToolSource
{
    private readonly IReadOnlyList<ModuleKind> _modules = [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];

    public IEnumerable<AgentTool> Tools
    {
        get
        {
            var api = catalog.Catalog;
            var runner = new ScriptToolRunner(workspace, scripts);
            foreach (var module in _modules)
            {
                foreach (var root in api.Roots.Where(root => root.Module == module))
                {
                    var exposed = root.Members.Where(access.IsExposed).ToList();
                    var properties = exposed.Where(member => member.Kind == ApiSymbolKind.Property).ToList();
                    if (properties.Count > 0)
                        yield return new ScriptPropertiesTool(runner, modules.Prefix(module), module, root, properties);
                    foreach (var method in exposed.Where(member => member.Kind == ApiSymbolKind.Method))
                        yield return new ScriptMethodTool(runner, modules.Prefix(module), module, root, method, access.AccessOf(method));
                }
            }
        }
    }
}

public sealed class ScriptToolRunner(IAgentWorkspace workspace, IAgentScripts scripts)
{
    public async Task<AgentToolResult> RunAsync(ModuleKind module, string? document, string? undoLabel, string source, bool returnsValue, AgentAccessLevel access, CancellationToken ct)
    {
        var outcome = await workspace.WithDocumentAsync(
            new AgentTarget(document, module), undoLabel,
            (_, token) => scripts.RunAsync(module, source, access, token), ct).ConfigureAwait(true);

        if (!outcome.Succeeded) return AgentToolResult.Failure(outcome.Error ?? "The call failed.");
        if (outcome.Value is null) return returnsValue ? AgentToolResult.Json(null) : AgentToolResult.Text("Done.");
        return AgentToolResult.Json(JsonSerializer.SerializeToNode(outcome.Value, _json));
    }

    private readonly JsonSerializerOptions _json = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
}

public sealed class ScriptCallSource
{
    public string Call(string root, string member, IReadOnlyList<ApiParameter> parameters, AgentArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(arguments);

        var values = new List<string>();
        string? missing = null;
        foreach (var parameter in parameters)
        {
            if (arguments.Raw(parameter.Name) is not { } value)
            {
                missing ??= parameter.Name;
                continue;
            }

            if (missing is not null) throw new AgentArgumentException($"The argument '{missing}' is required before '{parameter.Name}'.");
            values.Add(value.ToJsonString());
        }

        return $"{root}.{member}({string.Join(", ", values)})";
    }

    public string Properties(string root, IEnumerable<ApiSymbol> properties) =>
        $"({{ {string.Join(", ", properties.Select(property => $"{property.Name}: {root}.{property.Name}"))} }})";
}

internal sealed class ScriptMethodTool(
    ScriptToolRunner runner, string prefix, ModuleKind module, ApiSymbol root, ApiSymbol method, AgentAccessLevel access) : AgentTool
{
    public override string Name => $"{prefix}_{root.Name}_{method.Name}";

    public override string Title => method.Title is { Length: > 0 } title ? title : Name;

    public override string Description =>
        $"{Brand}: {method.Signature ?? method.Name}. {method.Documentation}".Trim() +
        (access == AgentAccessLevel.Read ? string.Empty
            : method.IsOutsideHistory ? " Not an undoable step: the user confirms it first."
            : " One undoable step, marked as made by the agent.") +
        " Same API as the macros of " + Brand + " ('" + root.Name + "' object).";

    public override AgentAccessLevel Access => access;

    public override AgentToolAnnotations Annotations => new(access == AgentAccessLevel.Read, access == AgentAccessLevel.Files);

    public override JsonObject InputSchema()
    {
        var schema = new SchemaBuilder();
        foreach (var parameter in method.Parameters)
            schema.Any(parameter.Name, parameter.Documentation ?? parameter.Name, required: !parameter.IsOptional);
        schema.String("document", $"Optional id of the {Brand} document (list_documents); the active one when omitted.");
        return schema.Build();
    }

    public override Task<AgentConfirmationNeed?> ConfirmationAsync(AgentArguments arguments, CancellationToken ct) =>
        Task.FromResult(access == AgentAccessLevel.Files
            ? new AgentConfirmationNeed(AgentConfirmationKind.FileAccess, Subject(arguments))
            : method.IsOutsideHistory
                ? new AgentConfirmationNeed(AgentConfirmationKind.Irreversible, Subject(arguments))
                : null);

    private string Subject(AgentArguments arguments) => $"{root.Name}.{method.Name} {arguments.Values.ToJsonString()}";

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var result = await runner.RunAsync(
            module, arguments.OptionalText("document"), access == AgentAccessLevel.Read ? null : Title,
            new ScriptCallSource().Call(root.Name, method.Name, method.Parameters, arguments), method.TypeName is not (null or "void"), access, ct).ConfigureAwait(true);
        return result.IsError && result.Content is [AgentText { Text: var error }] && method.Signature is { } signature
            ? AgentToolResult.Failure(string.Join(" ", [error, $"Expected {signature}.", .. method.Parameters.Select(parameter => $"{parameter.Name}: {parameter.Documentation ?? parameter.TypeName}")]))
            : result;
    }

    private string Brand => ModuleIdentity.For(module).BrandName;
}

internal sealed class ScriptPropertiesTool(
    ScriptToolRunner runner, string prefix, ModuleKind module, ApiSymbol root, IReadOnlyList<ApiSymbol> properties) : AgentTool
{
    public override string Name => $"{prefix}_{root.Name}_properties";

    public override string Title => root.Title is { Length: > 0 } title ? title : Name;

    public override string Description =>
        $"{ModuleIdentity.For(module).BrandName}: reads the properties of the '{root.Name}' object " +
        $"({string.Join(", ", properties.Select(property => property.Name))}). {root.Documentation}".Trim();

    public override AgentAccessLevel Access => AgentAccessLevel.Read;

    public override JsonObject InputSchema() => new SchemaBuilder()
        .String("document", "Optional id of the document (list_documents); the active one when omitted.")
        .Build();

    public override Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct) =>
        runner.RunAsync(module, arguments.OptionalText("document"), null, new ScriptCallSource().Properties(root.Name, properties), true, AgentAccessLevel.Read, ct);
}
