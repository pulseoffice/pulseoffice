using System.Globalization;

namespace PulseOffice.Text.Rich;

public interface ITextCultures
{
    CultureInfo For(params IEnumerable<string?> languages);
}

public sealed class TextCultures(CultureInfo undeclared) : ITextCultures
{
    private readonly HashSet<string> _noLanguage = new(StringComparer.OrdinalIgnoreCase) { "zxx", "none", "und", "x-none" };

    public TextCultures() : this(CultureInfo.CurrentCulture)
    {
    }

    public CultureInfo For(params IEnumerable<string?> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        foreach (var language in languages)
            if (Parse(language) is { } culture) return culture;
        return undeclared;
    }

    private CultureInfo? Parse(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        var tag = language.Trim().Replace('_', '-');
        if (_noLanguage.Contains(tag)) return null;
        try
        {
            var culture = CultureInfo.GetCultureInfo(tag);
            return culture.Equals(CultureInfo.InvariantCulture) ? null : culture;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
