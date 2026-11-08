using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting;

public enum ScriptLanguage { JavaScript, Python }

public enum ApiSymbolKind
{
    Namespace, Class, Interface, Enum, EnumMember,
    Method, Property, Event, Field, Constructor, Parameter, Constant,
}

public sealed record ApiSymbol
{
    public required string Name { get; init; }
    public required ApiSymbolKind Kind { get; init; }

    public required string QualifiedName { get; init; }

    public ModuleKind Module { get; init; } = ModuleKind.Shell;

    public string? TypeName { get; init; }
    public string? Documentation { get; init; }

    public string? Title { get; init; }

    public string? Signature { get; init; }

    public IReadOnlyList<ApiParameter> Parameters { get; init; } = [];
    public IReadOnlyList<ApiSymbol> Members { get; init; } = [];

    public bool IsAlias { get; init; }

    public bool IsStatic { get; init; }
    public bool IsReadOnly { get; init; }

    public bool IsOutsideHistory { get; init; }

    public string? SinceVersion { get; init; }

    public string? ObsoleteReplacement { get; init; }
}

public sealed record ApiParameter(string Name, string TypeName, bool IsOptional = false, string? Documentation = null);

public sealed class ScriptApiCatalog
{
    private readonly List<ApiSymbol> _roots = [];

    public IReadOnlyList<ApiSymbol> Roots => _roots;

    public void Register(ApiSymbol root) => _roots.Add(root);

    public IEnumerable<ApiSymbol> RootsFor(ModuleKind module) =>
    _roots.Where(s => s.Module == ModuleKind.Shell || s.Module == module);

    public ApiSymbol? Resolve(string qualifiedPath, ModuleKind module)
    {
        var parts = qualifiedPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var current = RootsFor(module).FirstOrDefault(s =>
            string.Equals(s.Name, parts[0], StringComparison.Ordinal));

        for (var i = 1; i < parts.Length && current is not null; i++)
            current = current.Members.FirstOrDefault(m =>
                string.Equals(m.Name, parts[i], StringComparison.Ordinal));

        return current;
    }

    public IEnumerable<ApiSymbol> Complete(string prefixPath, ModuleKind module)
    {
        if (string.IsNullOrEmpty(prefixPath)) return RootsFor(module);
        return Resolve(prefixPath, module)?.Members ?? [];
    }

    public IReadOnlySet<string> AllSymbolNames(ModuleKind module)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in RootsFor(module)) Walk(root);
        return names;

        void Walk(ApiSymbol symbol)
        {
            names.Add(symbol.Name);
            foreach (var member in symbol.Members) Walk(member);
        }
    }
}
