using PulseOffice.Foundation.Modules;

namespace PulseOffice.Agent.Hosting;

public interface IAgentModuleNames
{
    string Prefix(ModuleKind module);

    ModuleKind? Parse(string? name);

    IReadOnlyList<string> Names { get; }
}

public sealed class AgentModuleNames : IAgentModuleNames
{
    private readonly IReadOnlyDictionary<ModuleKind, string> _names = new Dictionary<ModuleKind, string>
    {
        [ModuleKind.Sheets] = "ledger",
        [ModuleKind.Documents] = "folio",
        [ModuleKind.Presentations] = "deck",
    };

    public IReadOnlyList<string> Names => [.. _names.Values];

    public string Prefix(ModuleKind module) => _names.TryGetValue(module, out var name) ? name : "shell";

    public ModuleKind? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var (module, prefix) in _names)
            if (string.Equals(prefix, name.Trim(), StringComparison.OrdinalIgnoreCase)) return module;
        throw new Tools.AgentArgumentException($"Unknown module '{name}': use one of {string.Join(", ", _names.Values)}.");
    }
}
