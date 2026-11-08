using PulseOffice.Localization;

namespace PulseOffice.Shell.Tests;

public sealed class FakeTextCatalog : ITextCatalog
{
    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);

    public UiLanguage Language => UiLanguage.Italian;

    public event EventHandler? LanguageChanged;

    public string this[string key] =>
        _texts.TryGetValue(key, out var text) ? text : key[(key.LastIndexOf('.') + 1)..];

    public void Add(string key, string text) => _texts[key] = text;

    public bool Contains(string key) => _texts.ContainsKey(key);

    public string Format(string key, params object?[] arguments) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, this[key], arguments);

    public void Use(UiLanguage language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
}
