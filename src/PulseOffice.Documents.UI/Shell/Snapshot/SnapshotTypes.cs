using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public interface ISnapshotTypeCatalog
{
    IReadOnlyList<Type> DerivedFrom(Type baseType);
}

public sealed class SnapshotTypeCatalog : ISnapshotTypeCatalog
{
    private const string Prefix = "PulseOffice";
    private readonly Lazy<IReadOnlyList<Type>> _types;
    private readonly ConcurrentDictionary<Type, IReadOnlyList<Type>> _derived = new();

    public SnapshotTypeCatalog() => _types = new Lazy<IReadOnlyList<Type>>(Scan);

    public IReadOnlyList<Type> DerivedFrom(Type baseType) =>
        _derived.GetOrAdd(baseType, key => [.. _types.Value.Where(type => !type.IsAbstract && key.IsAssignableFrom(type)).OrderBy(type => type.FullName, StringComparer.Ordinal)]);

    private IReadOnlyList<Type> Scan()
    {
        var seen = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Stack<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Append(typeof(SnapshotTypeCatalog).Assembly).Where(Ours));
        while (pending.Count > 0)
        {
            var assembly = pending.Pop();
            if (!seen.TryAdd(assembly.FullName ?? assembly.GetName().Name ?? string.Empty, assembly)) continue;
            foreach (var reference in assembly.GetReferencedAssemblies().Where(name => name.Name?.StartsWith(Prefix, StringComparison.Ordinal) == true))
            {
                if (Load(reference) is { } loaded) pending.Push(loaded);
            }
        }
        return [.. seen.Values.SelectMany(Types).Where(type => type.IsClass && (type.IsPublic || type.IsNestedPublic))];
    }

    private bool Ours(Assembly assembly) => assembly.GetName().Name?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    private Assembly? Load(AssemblyName name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch (Exception error) when (error is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return null;
        }
    }

    private IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException error)
        {
            return error.Types.OfType<Type>();
        }
    }
}

public sealed class AbstractTypeContracts(ISnapshotTypeCatalog catalog)
{
    public void Apply(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || !info.Type.IsAbstract || info.Type.IsInterface) return;
        var derived = catalog.DerivedFrom(info.Type);
        if (derived.Count == 0) return;
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$kind",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var type in derived) options.DerivedTypes.Add(new JsonDerivedType(type, type.FullName!));
        info.PolymorphismOptions = options;
    }
}
