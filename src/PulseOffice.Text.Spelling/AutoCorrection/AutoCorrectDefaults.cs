using System.Globalization;

namespace PulseOffice.Text.Spelling;

public sealed record DefaultReplacement(string To, EntryOrigin Origin);

public sealed record AutoCorrectDefaultSet(
    IReadOnlyDictionary<string, DefaultReplacement> Replacements,
    IReadOnlyDictionary<string, EntryOrigin> Abbreviations,
    IReadOnlyDictionary<string, EntryOrigin> TwoCapitalWords)
{
    public IReadOnlyDictionary<string, string> ReplacementTexts =>
        Replacements.ToDictionary(pair => pair.Key, pair => pair.Value.To, StringComparer.OrdinalIgnoreCase);
}

public interface IAutoCorrectDefaults
{
    IReadOnlyList<string> Languages { get; }

    string Resolve(string? language);

    AutoCorrectDefaultSet For(string language);
}

public sealed class AutoCorrectDefaults : IAutoCorrectDefaults
{
    private const string Fallback = "en-US";

    private readonly IReadOnlyList<IAutoCorrectSource> _sources;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AutoCorrectDefaultSet> _cache = new(StringComparer.Ordinal);

    public AutoCorrectDefaults(IEnumerable<IAutoCorrectSource> sources)
    {
        _sources = [.. (sources ?? throw new ArgumentNullException(nameof(sources))).OrderBy(source => source.Priority)];
        if (!_sources.Any(source => source.Origin == EntryOrigin.Bundled))
            throw new ArgumentException("The bundled autocorrect source is required.", nameof(sources));
    }

    public IReadOnlyList<string> Languages =>
        [.. _sources.SelectMany(source => source.Languages).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)];

    public string Resolve(string? language)
    {
        var tag = (string.IsNullOrWhiteSpace(language) ? CultureInfo.CurrentCulture.Name : language).Replace('_', '-');
        var available = Languages;
        var primary = tag.Split('-')[0];
        var region = CultureInfo.CurrentCulture.Name.Split('-') is [_, var current, ..] ? current : string.Empty;

        return Find(available, tag)
            ?? Find(available, primary)
            ?? Find(available, $"{primary}-{region}")
            ?? Find(available, $"{primary}-{primary.ToUpperInvariant()}")
            ?? (Fallback.StartsWith(primary + "-", StringComparison.OrdinalIgnoreCase) ? Find(available, Fallback) : null)
            ?? available.FirstOrDefault(candidate => candidate.StartsWith(primary + "-", StringComparison.OrdinalIgnoreCase))
            ?? Find(available, Fallback)
            ?? available.FirstOrDefault()
            ?? Fallback;
    }

    private string? Find(IEnumerable<string> available, string tag) =>
        available.FirstOrDefault(candidate => string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase));

    public AutoCorrectDefaultSet For(string language)
    {
        var resolved = Resolve(language);

        lock (_gate)
        {
            if (_cache.TryGetValue(resolved, out var cached)) return cached;

            var replacements = new Dictionary<string, DefaultReplacement>(StringComparer.OrdinalIgnoreCase);
            var abbreviations = new Dictionary<string, EntryOrigin>(StringComparer.OrdinalIgnoreCase);
            var twoCapitals = new Dictionary<string, EntryOrigin>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in _sources)
            {
                if (source.Lists(resolved) is not { } lists) continue;

                foreach (var (from, to) in lists.Replacements)
                    replacements.TryAdd(from, new DefaultReplacement(to, source.Origin));
                foreach (var word in lists.Abbreviations) abbreviations.TryAdd(word, source.Origin);
                foreach (var word in lists.TwoCapitalWords) twoCapitals.TryAdd(word, source.Origin);
            }

            var set = new AutoCorrectDefaultSet(replacements, abbreviations, twoCapitals);
            _cache[resolved] = set;
            return set;
        }
    }
}
