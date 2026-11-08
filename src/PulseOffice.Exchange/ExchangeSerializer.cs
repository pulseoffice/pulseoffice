using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Exchange;

public interface IExchangeSerializer
{
    byte[] Write(ExchangePackage package);

    ExchangePackage? Read(ReadOnlyMemory<byte> data);
}

public interface IExchangeTypeCatalog
{
    IReadOnlyList<Type> DerivedFrom(Type type);
}

public sealed class LoadedExchangeTypeCatalog : IExchangeTypeCatalog
{
    private readonly ConcurrentDictionary<Type, (int Assemblies, IReadOnlyList<Type> Types)> _derived = new();

    public IReadOnlyList<Type> DerivedFrom(Type type)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Length;
        if (_derived.TryGetValue(type, out var known) && known.Assemblies == loaded) return known.Types;
        var discovered = Discover(type);
        _derived[type] = (loaded, discovered);
        return discovered;
    }

    private IReadOnlyList<Type> Discover(Type type) =>
        [.. AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && References(assembly, type.Assembly))
            .SelectMany(Exported)
            .Where(candidate => candidate is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } && type.IsAssignableFrom(candidate) && candidate != type)
            .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal)];

    private bool References(Assembly assembly, Assembly target) =>
        assembly == target || assembly.GetReferencedAssemblies().Any(name => AssemblyName.ReferenceMatchesDefinition(name, target.GetName()));

    private IEnumerable<Type> Exported(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException failure)
        {
            return failure.Types.OfType<Type>().Where(candidate => candidate.IsPublic);
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }
}

public sealed class JsonExchangeSerializer : IExchangeSerializer
{
    public const int MaximumBytes = 256 * 1024 * 1024;

    private const int Version = 1;

    private readonly JsonSerializerOptions _options;

    private readonly IExchangeContentGuard _guard;

    public JsonExchangeSerializer(IExchangeTypeCatalog types, IExchangeContentGuard guard)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(guard);
        _guard = guard;
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => Polymorphic(info, types));
        _options = new JsonSerializerOptions
        {
            IncludeFields = true,
            TypeInfoResolver = resolver,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false), new ExchangeScalarConverter() },
            MaxDepth = 128,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
    }

    public byte[] Write(ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var envelope = new Envelope(Version, package.Id, package.Origin, package.Content);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
    }

    public ExchangePackage? Read(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty || data.Length > MaximumBytes) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(data.Span, _options);
            if (envelope is not { Version: Version } || string.IsNullOrEmpty(envelope.Id)) return null;
            if (envelope.Origin is { } origin && !Enum.IsDefined(origin)) return null;
            var content = envelope.Content is null ? null : _guard.Admit(envelope.Content);
            if (envelope.Content is not null && content is null) return null;
            return new ExchangePackage { Id = envelope.Id, Origin = envelope.Origin, Content = content };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void Polymorphic(JsonTypeInfo info, IExchangeTypeCatalog types)
    {
        if (info.Kind != JsonTypeInfoKind.Object || !(info.Type.IsAbstract || info.Type.IsInterface) || info.Type == typeof(object)) return;
        var derived = types.DerivedFrom(info.Type);
        if (derived.Count == 0) return;
        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$type",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var type in derived)
            info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, type.FullName!));
    }

    private sealed record Envelope(int Version, string Id, ModuleKind? Origin = null, ExchangeContent? Content = null);
}
