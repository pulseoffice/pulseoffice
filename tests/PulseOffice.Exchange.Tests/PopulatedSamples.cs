using System.Collections;
using System.Reflection;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Exchange.Tests;

public sealed class PopulatedSamples(IExchangeTypeCatalog types)
{
    private const int MaximumDepth = 6;

    private const int HardDepth = 24;

    private int _counter;

    public IReadOnlyList<Type> ObjectKinds() => types.DerivedFrom(typeof(EmbeddedObject));

    public object? Create(Type type, int depth = 0)
    {
        _counter++;
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null) return Create(underlying, depth);
        if (type == typeof(string)) return $"v{_counter}";
        if (type == typeof(bool)) return true;
        if (type == typeof(int)) return 2 + _counter % 5;
        if (type == typeof(long)) return 3L + _counter % 5;
        if (type == typeof(double)) return 0.25 + _counter % 3;
        if (type == typeof(float)) return 0.5f;
        if (type == typeof(decimal)) return 1.5m;
        if (type == typeof(byte)) return (byte)7;
        if (type == typeof(char)) return 'x';
        if (type == typeof(Guid)) return Guid.Parse("11111111-2222-3333-4444-555555555555");
        if (type == typeof(DateTime)) return new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        if (type == typeof(DateTimeOffset)) return new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        if (type == typeof(TimeSpan)) return TimeSpan.FromSeconds(3);
        if (type == typeof(byte[])) return new byte[] { 1, 2, 3 };
        if (type == typeof(ReadOnlyMemory<byte>)) return new ReadOnlyMemory<byte>([1, 2, 3]);
        if (type.IsEnum) return Enum.GetValues(type).Cast<object>().Skip(1).FirstOrDefault() ?? Activator.CreateInstance(type);
        if (type == typeof(object)) return null;
        if (type.IsArray) return Collection(type.GetElementType()!, depth, type);
        if (type.IsGenericType && ElementOf(type) is { } element)
            return IsMap(type) && KeyValueOf(type) is { } pair ? Map(type, pair.Key, pair.Value, depth) : Collection(element, depth, type);
        if (depth >= HardDepth) return type.IsValueType ? Activator.CreateInstance(type) : null;
        if (type.IsAbstract || type.IsInterface)
        {
            var derived = types.DerivedFrom(type);
            return derived.Count == 0 ? null : Create(derived[_counter % derived.Count], depth + 1);
        }
        return Construct(type, depth);
    }

    private object? Construct(Type type, int depth)
    {
        var constructor = type.GetConstructors().Where(candidate => !Copy(candidate, type)).OrderByDescending(candidate => candidate.GetParameters().Length).FirstOrDefault();
        if (constructor is null) return type.IsValueType ? Activator.CreateInstance(type) : null;
        var arguments = constructor.GetParameters().Select(parameter => Create(parameter.ParameterType, depth + 1)).ToArray();
        object instance;
        try
        {
            instance = constructor.Invoke(arguments);
        }
        catch (TargetInvocationException)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod is not { IsPublic: true } || property.GetIndexParameters().Length > 0) continue;
            if (constructor.GetParameters().Any(parameter => string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase))) continue;
            var value = Create(property.PropertyType, depth + 1);
            if (value is null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null) continue;
            try
            {
                property.SetValue(instance, value);
            }
            catch (TargetInvocationException)
            {
            }
            catch (ArgumentException)
            {
            }
        }
        return instance;
    }

    private bool Copy(ConstructorInfo constructor, Type type) =>
        constructor.GetParameters() is [{ } only] && only.ParameterType == type;

    private Type? ElementOf(Type type) =>
        type.GetGenericTypeDefinition() is var definition
        && (definition == typeof(IReadOnlyList<>) || definition == typeof(IList<>) || definition == typeof(List<>) || definition == typeof(IEnumerable<>)
            || definition == typeof(IReadOnlyCollection<>) || definition == typeof(ICollection<>) || definition == typeof(HashSet<>) || definition == typeof(ISet<>)
            || definition == typeof(IReadOnlySet<>) || definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(Dictionary<,>))
            ? type.GetGenericArguments()[0]
            : null;

    private bool IsMap(Type type) =>
        type.GetGenericTypeDefinition() is var definition && (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(Dictionary<,>));

    private KeyValuePair<Type, Type>? KeyValueOf(Type type) =>
        type.GetGenericArguments() is [var key, var value] ? new KeyValuePair<Type, Type>(key, value) : null;

    private object Collection(Type element, int depth, Type target)
    {
        var count = depth >= MaximumDepth ? 0 : 2;
        var array = Array.CreateInstance(element, count);
        for (var index = 0; index < count; index++)
        {
            var item = Create(element, depth + 1);
            if (item is not null || !element.IsValueType) array.SetValue(item, index);
        }
        if (target.IsArray) return array;
        var definition = target.GetGenericTypeDefinition();
        if (definition == typeof(HashSet<>) || definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>))
            return Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(element), array)!;
        return Activator.CreateInstance(typeof(List<>).MakeGenericType(element), array)!;
    }

    private object Map(Type target, Type key, Type value, int depth)
    {
        var map = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(key, value))!;
        if (depth >= MaximumDepth) return map;
        for (var index = 0; index < 2; index++)
            if (Create(key, depth + 1) is { } entry && !map.Contains(entry)) map[entry] = Create(value, depth + 1);
        return map;
    }
}
