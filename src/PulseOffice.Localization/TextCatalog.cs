using System.Collections.Concurrent;
using System.Reflection;
using System.Resources;
using System.Text.Json;

namespace PulseOffice.Localization;

public interface ITextCatalog
{
    UiLanguage Language { get; }

    string this[string key] { get; }

    string Format(string key, params object?[] arguments);

    bool Contains(string key);

    void Use(UiLanguage language);

    event EventHandler? LanguageChanged;
}

public sealed class TextCatalog : ITextCatalog
{
    private readonly ConcurrentDictionary<UiLanguage, IReadOnlyDictionary<string, string>> _loaded = new();
    private IReadOnlyDictionary<string, string> _current = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> _fallback = new Dictionary<string, string>();

    public TextCatalog(UiLanguage language = UiLanguage.System) => Use(language);

    public UiLanguage Language { get; private set; } = UiLanguage.English;

    public event EventHandler? LanguageChanged;

    public void Use(UiLanguage language)
    {
        var resolved = language.Resolve();

        Language = resolved;
        _current = Load(resolved);

        _fallback = resolved == UiLanguage.English ? _current : Load(UiLanguage.English);

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(key);

            if (_current.TryGetValue(key, out var text)) return text;
            if (_fallback.TryGetValue(key, out var english)) return english;

            return $"⟦{key}⟧";
        }
    }

    public string Format(string key, params object?[] arguments) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, this[CountedKey(key, arguments)], arguments);

    private string CountedKey(string key, object?[] arguments)
    {
        var singular = key + ".one";
        return arguments is [var count, ..] && IsOne(count) && Contains(singular) ? singular : key;
    }

    private static bool IsOne(object? count) => count is 1 or 1L or (short)1 or (byte)1 or 1U or 1UL;

    public bool Contains(string key) =>
        _current.ContainsKey(key) || _fallback.ContainsKey(key);

    public IReadOnlyCollection<string> KeysIn(UiLanguage language) => Load(language).Keys.ToList();

    private IReadOnlyDictionary<string, string> Load(UiLanguage language) =>
        _loaded.GetOrAdd(language, ReadResource);

    private static IReadOnlyDictionary<string, string> ReadResource(UiLanguage language)
    {
        var assembly = typeof(TextCatalog).Assembly;
        var name = $"PulseOffice.Localization.Resources.{language.ToCode()}.json";

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new MissingManifestResourceException(
                $"Missing text catalog for language '{language.ToCode()}'.");

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Unreadable text catalog: {name}.");

        return parsed;
    }
}
