namespace PulseOffice.Formats;

public sealed class TokenMap<TEnum> where TEnum : struct, Enum
{
    private readonly IReadOnlyList<(TEnum Value, string Token)> _entries;
    private readonly TEnum _fallback;
    private readonly string _fallbackToken;

    public TokenMap(TEnum fallback, string fallbackToken, params (TEnum Value, string Token)[] entries)
    {
        _fallback = fallback;
        _fallbackToken = fallbackToken;
        _entries = entries;
    }

    public string Token(TEnum value) =>
        _entries.Where(entry => EqualityComparer<TEnum>.Default.Equals(entry.Value, value))
            .Select(entry => entry.Token).FirstOrDefault() ?? _fallbackToken;

    public TEnum Value(string? token) => Find(token) ?? _fallback;

    public TEnum? Find(string? token) =>
        _entries.Where(entry => entry.Token == token).Select(entry => (TEnum?)entry.Value).FirstOrDefault();
}
