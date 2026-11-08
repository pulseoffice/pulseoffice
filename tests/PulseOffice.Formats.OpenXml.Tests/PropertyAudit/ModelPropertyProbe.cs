using System.Collections;
using System.Reflection;

namespace PulseOffice.Formats.OpenXml.Tests.PropertyAudit;

public sealed record PropertyVariant(string Path, object Instance);

public sealed class ModelPropertyProbe(IReadOnlyDictionary<string, object> samples)
{
    private const int MaxDepth = 3;

    public IEnumerable<PropertyVariant> Variants(object baseline) => Variants(baseline, string.Empty, 0);

    public object? Read(object root, string path)
    {
        object? current = root;
        foreach (var name in path.Split('.'))
            current = current is null ? null : current.GetType().GetProperty(name)!.GetValue(current);
        return current;
    }

    public bool Same(object? expected, object? actual) => (expected, actual) switch
    {
        (null, null) => true,
        (null, _) or (_, null) => false,
        (double left, double right) => Math.Abs(left - right) <= 1e-3 * Math.Max(1, Math.Abs(left)),
        (string left, string right) when left.StartsWith('#') => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
        (string, _) => expected.Equals(actual),
        (IEnumerable left, IEnumerable right) => left.Cast<object?>().Count() == right.Cast<object?>().Count()
            && left.Cast<object?>().Zip(right.Cast<object?>()).All(pair => Same(pair.First, pair.Second)),
        _ when IsComposite(expected.GetType()) || IsRecordStruct(expected.GetType()) => expected.GetType() == actual.GetType()
            && Settable(expected.GetType()).All(property => Same(property.GetValue(expected), property.GetValue(actual))),
        _ => expected.Equals(actual),
    };

    public object Build(Type type, string path)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (Given(path, target) is { } given) return given is object[] choices ? choices[0] : given;
        if (target == typeof(bool)) return true;
        if (target == typeof(int)) return 3;
        if (target == typeof(uint)) return 0xFF3366CCu;
        if (target == typeof(double)) return 1.5;
        if (target == typeof(char)) return ',';
        if (target == typeof(string)) return "Probe";
        if (target.IsEnum) return Enum.GetValues(target).Cast<object>().Skip(1).FirstOrDefault() ?? Enum.GetValues(target).GetValue(0)!;
        if (ListElement(target) is { } element)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
            list.Add(Build(element, path + ".Item"));
            return list;
        }

        var constructor = target.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length).First();
        var instance = constructor.Invoke([.. constructor.GetParameters().Select(parameter =>
            Build(parameter.ParameterType, Join(path, Pascal(parameter.Name!))))]);
        foreach (var property in Settable(target).Where(property => Default(property.GetValue(instance))))
            property.SetValue(instance, Build(property.PropertyType, Join(path, property.Name)));
        return instance;
    }

    private object? Given(string path, Type type) =>
        samples.TryGetValue(path, out var exact) ? exact
        : samples.TryGetValue("*." + path.Split('.')[^1], out var named) ? named
        : samples.TryGetValue("@" + type.Name, out var typed) ? typed
        : null;

    private IEnumerable<PropertyVariant> Variants(object baseline, string prefix, int depth)
    {
        foreach (var property in Settable(baseline.GetType()))
        {
            var path = Join(prefix, property.Name);
            var current = property.GetValue(baseline);
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (Given(path, type) is object[] choices)
            {
                foreach (var choice in choices.Where(choice => !Same(choice, current)))
                    yield return new PropertyVariant(path, With(baseline, property, choice));
                continue;
            }

            if (IsComposite(type) && depth < MaxDepth)
            {
                var rich = Build(type, path);
                if (current is null) yield return new PropertyVariant(path, With(baseline, property, rich));
                else if (!Same(current, rich)) yield return new PropertyVariant(path, With(baseline, property, rich));
                foreach (var nested in Variants(rich, path, depth + 1))
                    yield return new PropertyVariant(nested.Path, With(baseline, property, nested.Instance));
                continue;
            }

            foreach (var sample in Alternatives(property.PropertyType, current, path))
                yield return new PropertyVariant(path, With(baseline, property, sample));
        }
    }

    private IEnumerable<object> Alternatives(Type declared, object? current, string path)
    {
        var type = Nullable.GetUnderlyingType(declared) ?? declared;
        if (type.IsEnum)
        {
            foreach (var value in Enum.GetValues(type).Cast<object>().Where(value => !value.Equals(current)))
                yield return value;
            yield break;
        }

        var sample = Build(declared, path);
        yield return Same(sample, current) ? Shift(sample) : sample;
    }

    private object Shift(object sample) => sample switch
    {
        bool flag => !flag,
        int number => number + 2,
        uint color => color ^ 0x00FFFFFFu,
        double number => number <= 1 ? number / 2 : number + 1,
        string text when text.StartsWith('#') => text.Equals("#00AA55", StringComparison.OrdinalIgnoreCase) ? "#AA0055" : "#00AA55",
        string text => text + "2",
        _ => sample,
    };

    private object With(object baseline, PropertyInfo property, object? value)
    {
        var clone = baseline.GetType().GetMethod("<Clone>$")!.Invoke(baseline, null)!;
        property.SetValue(clone, value);
        return clone;
    }

    private IEnumerable<PropertyInfo> Settable(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
            .Where(property => property.Name != "EqualityContract");

    private bool IsRecordStruct(Type type) =>
        type.IsValueType && !type.IsPrimitive && !type.IsEnum && type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance) is not null;

    private bool IsComposite(Type type) =>
        !type.IsPrimitive && !type.IsEnum && type != typeof(string) && type != typeof(decimal)
        && (ListElement(type) is null) && type.GetMethod("<Clone>$") is not null;

    private Type? ListElement(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ? type.GetGenericArguments()[0] : null;

    private bool Default(object? value) => value switch
    {
        null => true,
        bool flag => !flag,
        int number => number == 0,
        double number => number == 0,
        uint number => number == 0,
        string text => text.Length == 0,
        _ => value.GetType().IsValueType && value.Equals(Activator.CreateInstance(value.GetType())),
    };

    private string Join(string prefix, string name) => prefix.Length == 0 ? name : prefix + "." + name;

    private string Pascal(string name) => char.ToUpperInvariant(name[0]) + name[1..];
}
