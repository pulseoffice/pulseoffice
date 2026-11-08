using System.Globalization;

namespace PulseOffice.Settings.Navigation;

public sealed record SettingState(bool IsModified, bool IsLocked, bool RequiresRestart)
{
    public bool CanReset => IsModified && !IsLocked;
}

public interface ISettingValueComparer
{
    bool Same(object? left, object? right);
}

public sealed class SettingValueComparer : ISettingValueComparer
{
    public bool Same(object? left, object? right)
    {
        if (left is null || right is null) return left is null && right is null;
        if (Number(left) is { } a && Number(right) is { } b) return a == b;

        return Equals(left, right);
    }

    private double? Number(object value) => value switch
    {
        bool or string => null,
        IConvertible convertible when value.GetType().IsPrimitive || value is decimal
            => convertible.ToDouble(CultureInfo.InvariantCulture),
        _ => null,
    };
}

public interface ISettingStates
{
    SettingState Of(SettingDefinition definition);

    bool AnyResettable(IEnumerable<SettingDefinition> definitions);

    void Reset(SettingDefinition definition);

    int ResetAll(IEnumerable<SettingDefinition> definitions);
}

public sealed class SettingStates(ISettingsService settings, ISettingValueComparer values) : ISettingStates
{
    private readonly ISettingsService _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ISettingValueComparer _values = values ?? throw new ArgumentNullException(nameof(values));

    public SettingState Of(SettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var modified = !_values.Same(_settings.Get<object>(definition.Key), definition.DefaultValue);
        return new SettingState(modified, _settings.IsLocked(definition.Key), definition.RequiresRestart);
    }

    public bool AnyResettable(IEnumerable<SettingDefinition> definitions) =>
        definitions.Any(definition => Of(definition).CanReset);

    public void Reset(SettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!Of(definition).CanReset) return;

        _settings.Reset(definition.Key);
    }

    public int ResetAll(IEnumerable<SettingDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var resettable = definitions.Where(definition => Of(definition).CanReset).ToList();
        resettable.ForEach(Reset);

        return resettable.Count;
    }
}
