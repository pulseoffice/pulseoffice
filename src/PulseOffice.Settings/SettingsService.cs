using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PulseOffice.Localization;

namespace PulseOffice.Settings;

public sealed record SettingChangedEventArgs(string Key, object? Value, SettingScope Scope);

public interface ISettingsStore
{
    SettingScope Scope { get; }
    bool TryGet(string key, out object? value);
    void Set(string key, object? value);
    void Remove(string key);
    IReadOnlyCollection<string> Keys { get; }
}

public sealed class InMemorySettingsStore(SettingScope scope) : ISettingsStore
{
    private readonly ConcurrentDictionary<string, object?> _values = new(StringComparer.Ordinal);

    public SettingScope Scope { get; } = scope;

    public bool TryGet(string key, out object? value) => _values.TryGetValue(key, out value);
    public void Set(string key, object? value) => _values[key] = value;
    public void Remove(string key) => _values.TryRemove(key, out _);
    public IReadOnlyCollection<string> Keys => _values.Keys.ToList();
}

public interface ISettingsService
{
    IReadOnlyCollection<SettingDefinition> Definitions { get; }

    SettingDefinition? Find(string key);

    T Get<T>(string key);

    SettingScope EffectiveScope(string key);

    bool IsLocked(string key);

    void Set(string key, object? value, SettingScope scope = SettingScope.User);

    void Reset(string key, SettingScope scope = SettingScope.User);

    IEnumerable<SettingDefinition> Search(string query);

    event EventHandler<SettingChangedEventArgs>? Changed;
}

public sealed class SettingsService : ISettingsService
{
    private readonly Dictionary<string, SettingDefinition> _definitions;
    private readonly IReadOnlyList<ISettingsStore> _stores;
    private readonly ILogger<SettingsService> _logger;

    private readonly ITextCatalog _texts;

    public SettingsService(
        IEnumerable<ISettingsProvider> providers,
        IEnumerable<ISettingsStore> stores,
        ITextCatalog texts,
        ILogger<SettingsService> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(stores);

        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _logger = logger;

        _definitions = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);
        foreach (var provider in providers)
            foreach (var definition in provider.GetSettings())
                if (!_definitions.TryAdd(definition.Key, definition))
                    throw new InvalidOperationException($"Duplicate setting: '{definition.Key}'.");

        _stores = [.. stores.OrderBy(s => SettingScopes.Precedence(s.Scope))];
    }

    public IReadOnlyCollection<SettingDefinition> Definitions => _definitions.Values;

    public SettingDefinition? Find(string key) => _definitions.GetValueOrDefault(key);

    public event EventHandler<SettingChangedEventArgs>? Changed;

    public T Get<T>(string key)
    {
        var definition = Require(key);
        var value = Resolve(key) ?? definition.DefaultValue;
        if (!definition.IsValid(value) && definition.IsValid(definition.DefaultValue)) value = definition.DefaultValue;

        if (value is T typed) return typed;
        if (TryConvertNumber<T>(value, out var converted)) return converted;

        _logger.LogWarning(
            "Setting {Key}: expected {Expected}, found {Actual}. Falling back to the default.",
            key, typeof(T).Name, value.GetType().Name);

        if (definition.DefaultValue is T fallback) return fallback;

        return TryConvertNumber<T>(definition.DefaultValue, out var convertedFallback)
            ? convertedFallback
            : throw new InvalidOperationException(
                $"Setting {key}: the default value cannot be read as {typeof(T).Name}.");
    }

    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal),
    ];

    private static bool TryConvertNumber<T>(object value, out T result)
    {
        result = default!;

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (!NumericTypes.Contains(target) || !NumericTypes.Contains(value.GetType())) return false;

        try
        {
            result = (T)Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }

    public SettingScope EffectiveScope(string key)
    {
        Require(key);

        for (var i = _stores.Count - 1; i >= 0; i--)
            if (_stores[i].TryGet(key, out _)) return _stores[i].Scope;

        return SettingScope.Factory;
    }

    public bool IsLocked(string key) =>
        _stores.Any(s => s.Scope == SettingScope.Policy && s.TryGet(key, out _));

    public void Set(string key, object? value, SettingScope scope = SettingScope.User)
    {
        var definition = Require(key);

        if (IsLocked(key))
            throw new InvalidOperationException(
                $"Setting '{key}' is locked by a policy and cannot be changed.");

        if (scope > definition.MaximumScope && scope != SettingScope.Policy)
            throw new InvalidOperationException(
                $"Setting '{key}' cannot be changed in scope {scope}.");

        if (!definition.IsValid(value))
            throw new ArgumentException($"Value not allowed for '{key}'.", nameof(value));

        Store(scope).Set(key, value);
        Changed?.Invoke(this, new SettingChangedEventArgs(key, value, scope));
    }

    public void Reset(string key, SettingScope scope = SettingScope.User)
    {
        Require(key);
        Store(scope).Remove(key);

        Changed?.Invoke(this, new SettingChangedEventArgs(key, Resolve(key), EffectiveScope(key)));
    }

    public IEnumerable<SettingDefinition> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _definitions.Values;

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return _definitions.Values.Where(definition =>
            terms.All(term =>
                _texts[definition.TitleKey].Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || definition.Key.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (_texts.Contains(definition.DescriptionKey)
                    && _texts[definition.DescriptionKey].Contains(term, StringComparison.CurrentCultureIgnoreCase))
                || definition.Keywords.Any(k => k.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                || (_texts.Contains(definition.KeywordsKey)
                    && _texts[definition.KeywordsKey].Contains(term, StringComparison.CurrentCultureIgnoreCase))));
    }

    private object? Resolve(string key)
    {
        for (var i = _stores.Count - 1; i >= 0; i--)
            if (_stores[i].TryGet(key, out var value)) return value;

        return null;
    }

    private SettingDefinition Require(string key) =>
        _definitions.TryGetValue(key, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown setting: '{key}'.");

    private ISettingsStore Store(SettingScope scope) =>
        _stores.FirstOrDefault(s => s.Scope == scope)
        ?? throw new InvalidOperationException($"No store registered for scope {scope}.");
}
