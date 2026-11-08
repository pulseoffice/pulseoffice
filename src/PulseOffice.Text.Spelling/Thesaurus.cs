namespace PulseOffice.Text.Spelling;

public sealed record ThesaurusMeaning(string PartOfSpeech, IReadOnlyList<string> Synonyms);

public interface IThesaurus
{
    LanguageTag Language { get; }

    IReadOnlyList<ThesaurusMeaning> Lookup(string word);
}

public interface IThesaurusProvider
{
    IThesaurus? For(LanguageTag language);
}

public sealed class MyThesThesaurus : IThesaurus
{
    private readonly Dictionary<string, List<ThesaurusMeaning>> _entries =
        new(StringComparer.InvariantCultureIgnoreCase);

    private MyThesThesaurus(LanguageTag language) => Language = language;

    public LanguageTag Language { get; }

    public static MyThesThesaurus Parse(LanguageTag language, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(lines);

        var thesaurus = new MyThesThesaurus(language);

        string? headword = null;
        var remaining = 0;
        var first = true;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;

            if (first)
            {
                first = false;
                if (!line.Contains('|', StringComparison.Ordinal)) continue;
            }

            if (remaining == 0)
            {
                var separator = line.LastIndexOf('|');

                if (separator < 0
                    || !int.TryParse(
                        line[(separator + 1)..],
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out remaining)
                    || remaining <= 0)
                {
                    headword = null;
                    remaining = 0;
                    continue;
                }

                headword = line[..separator];
                thesaurus._entries.TryAdd(headword, []);
                continue;
            }

            remaining--;

            if (headword is null) continue;

            var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) continue;

            var part = parts[0].Trim('(', ')');
            var synonyms = parts.Skip(1).Where(word => word.Length > 0).ToList();

            if (synonyms.Count > 0)
                thesaurus._entries[headword].Add(new ThesaurusMeaning(part, synonyms));
        }

        return thesaurus;
    }

    public IReadOnlyList<ThesaurusMeaning> Lookup(string word) =>
        string.IsNullOrWhiteSpace(word) || !_entries.TryGetValue(word.Trim(), out var meanings)
            ? []
            : meanings;
}
