using System.Globalization;

namespace PulseOffice.Text.Spelling;

public interface ITextLanguageResolver
{
    LanguageTag Resolve(params IEnumerable<string?> candidates);
}

public sealed class TextLanguageResolver : ITextLanguageResolver
{
    public LanguageTag Resolve(params IEnumerable<string?> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var chosen = candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        return LanguageTag.Parse(chosen ?? CultureInfo.CurrentCulture.Name);
    }
}
