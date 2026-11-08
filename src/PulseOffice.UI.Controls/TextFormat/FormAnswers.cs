namespace PulseOffice.UI.Controls.TextFormat;

public sealed class FormAnswers(IReadOnlyDictionary<string, object> values)
{
    private readonly ColorFieldValue _colors = new();

    public bool Has(string key) => values.ContainsKey(key);

    public string Text(string key) => values.TryGetValue(key, out var value) && value is string text ? text : string.Empty;

    public double Number(string key, double fallback) =>
        values.TryGetValue(key, out var value) && value is double number && double.IsFinite(number) ? number : fallback;

    public int Choice(string key, int count, int fallback = 0) =>
        values.TryGetValue(key, out var value) && value is int index && index >= 0 && index < count ? index : fallback;

    public bool Flag(string key, bool fallback = false) =>
        values.TryGetValue(key, out var value) && value is bool flag ? flag : fallback;

    public uint? Color(string key, uint? fallback) =>
        values.TryGetValue(key, out var value) && value is string text ? _colors.Argb(text) : fallback;

    public T? Value<T>(string key) where T : class =>
        values.TryGetValue(key, out var value) ? value as T : null;
}
