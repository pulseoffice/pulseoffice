using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public sealed class DefaultValueOmission
{
    public void Apply(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.IsAbstract || info.Type.IsInterface) return;
        if (Prototypes(info) is not var (plain, sample, parameters)) return;

        foreach (var property in info.Properties)
        {
            if (property.Get is not { } get || property.IsRequired) continue;
            var bound = parameters.Contains(MemberName(property));
            if (property.Set is null && !bound)
            {
                property.ShouldSerialize = (_, _) => false;
                continue;
            }

            if (!TryRead(get, plain, out var reference)) continue;
            if (!bound && (!TryRead(get, sample, out var other) || !Same(reference, other))) continue;

            var previous = property.ShouldSerialize;
            property.ShouldSerialize = previous is null
                ? (_, value) => !Same(value, reference)
                : (owner, value) => previous(owner, value) && !Same(value, reference);
        }
    }

    private (object Plain, object Sample, IReadOnlySet<string> Parameters)? Prototypes(JsonTypeInfo info)
    {
        try
        {
            if (info.ConstructorAttributeProvider is ConstructorInfo constructor && constructor.GetParameters() is { Length: > 0 } parameters)
            {
                var plain = constructor.Invoke([.. parameters.Select(PlainArgument)]);
                var sample = constructor.Invoke([.. parameters.Select(SampleArgument)]);
                return (plain, sample, parameters.Select(parameter => parameter.Name ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase));
            }

            if (info.CreateObject is { } create)
                return (create(), create(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception error) when (error is TargetInvocationException or ArgumentException or MemberAccessException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }

        return null;
    }

    private object? PlainArgument(ParameterInfo parameter) =>
        parameter.HasDefaultValue ? parameter.DefaultValue : DefaultOf(parameter.ParameterType);

    private object? SampleArgument(ParameterInfo parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (type == typeof(string)) return "\u0001";
        if (type == typeof(bool)) return true;
        if (type.IsEnum) return Enum.GetValues(type) is { Length: > 1 } values ? values.GetValue(1) : DefaultOf(parameter.ParameterType);
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(7, type, System.Globalization.CultureInfo.InvariantCulture);
        return PlainArgument(parameter);
    }

    private object? DefaultOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    private string MemberName(JsonPropertyInfo property) =>
        (property.AttributeProvider as MemberInfo)?.Name ?? property.Name;

    private bool TryRead(Func<object, object?> get, object owner, out object? value)
    {
        try
        {
            value = get(owner);
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or NullReferenceException or ArgumentException)
        {
            value = null;
            return false;
        }
    }

    private bool Same(object? value, object? reference)
    {
        if (value is null || reference is null) return value is null && reference is null;
        if (value is double number && reference is double expected) return BitConverter.DoubleToInt64Bits(number) == BitConverter.DoubleToInt64Bits(expected);
        if (value is float single && reference is float wanted) return BitConverter.SingleToInt32Bits(single) == BitConverter.SingleToInt32Bits(wanted);
        if (value is string || value.GetType().IsValueType) return value.Equals(reference);
        if (value is IEnumerable items && reference is IEnumerable others && !items.GetEnumerator().MoveNext() && !others.GetEnumerator().MoveNext())
            return value.GetType() == reference.GetType();
        return ReferenceEquals(value, reference) || (IsRecord(value.GetType()) && value.Equals(reference));
    }

    private bool IsRecord(Type type) => type.GetMethod("<Clone>$") is not null;
}
